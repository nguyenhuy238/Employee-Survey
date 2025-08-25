using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class PasswordResetService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<PasswordReset> _prRepo;
        private readonly IEmailSender _email;

        public PasswordResetService(
            IRepository<User> userRepo,
            IRepository<PasswordReset> prRepo,
            IEmailSender email)
        {
            _userRepo = userRepo;
            _prRepo = prRepo;
            _email = email;
        }

        // Tạo OTP 6 chữ số
        private static string NewOtp()
        {
            // an toàn hơn Random: dùng RNGCryptoServiceProvider
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var num = BitConverter.ToUInt32(bytes, 0) % 1_000_000u;
            return num.ToString("D6");
        }

        private static string NewToken() => Guid.NewGuid().ToString("N");

        public async Task RequestAsync(string email)
        {
            // Không tiết lộ email có tồn tại hay không
            var user = (await _userRepo.GetAllAsync()).FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                // Vẫn "giả vờ" gửi thành công để không lộ tài khoản
                return;
            }

            // Vô hiệu hóa các request cũ chưa dùng
            var all = await _prRepo.GetAllAsync();
            foreach (var i in all.Where(x => x.UserId == user.Id && !x.Used))
            {
                i.Used = true;
                await _prRepo.UpsertAsync(x => x.Id == i.Id, i);
            }

            var otp = NewOtp();
            var item = new PasswordReset
            {
                UserId = user.Id,
                Email = user.Email,
                OtpCode = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                ResetToken = "" // set sau khi verify OTP
            };
            await _prRepo.InsertAsync(item);

            var html = $@"
<h3>Mã xác thực đặt lại mật khẩu</h3>
<p>Mã OTP của bạn là: <b style=""font-size:20px"">{otp}</b></p>
<p>Mã có hiệu lực trong 10 phút.</p>
<p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>";

            await _email.SendAsync(user.Email, "[Employee Survey] OTP đặt lại mật khẩu", html);
        }

        public async Task<string?> VerifyOtpAsync(string email, string otp)
        {
            var user = (await _userRepo.GetAllAsync()).FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (user == null) return null;

            var prList = await _prRepo.GetAllAsync();
            var pr = prList
                .Where(x => x.UserId == user.Id && !x.Used)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (pr == null) return null;
            if (DateTime.UtcNow > pr.ExpiresAt) return null;

            // hạn chế brute-force
            if (pr.Attempts >= 5) return null;

            if (!string.Equals(pr.OtpCode, otp, StringComparison.Ordinal))
            {
                pr.Attempts++;
                await _prRepo.UpsertAsync(x => x.Id == pr.Id, pr);
                return null;
            }

            // OTP đúng -> phát hành reset token (dùng 1 lần)
            pr.ResetToken = NewToken();
            await _prRepo.UpsertAsync(x => x.Id == pr.Id, pr);
            return pr.ResetToken;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var prList = await _prRepo.GetAllAsync();
            var pr = prList.FirstOrDefault(x => !x.Used && x.ResetToken == token);
            if (pr == null) return false;
            if (DateTime.UtcNow > pr.ExpiresAt) return false;

            var user = await _userRepo.FirstOrDefaultAsync(u => ((User)u).Id == pr.UserId);
            if (user == null) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepo.UpsertAsync(u => ((User)u).Id == user.Id, user);

            // đánh dấu đã dùng
            pr.Used = true;
            await _prRepo.UpsertAsync(x => x.Id == pr.Id, pr);

            return true;
        }
    }
}
