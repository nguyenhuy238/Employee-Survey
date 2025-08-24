

using System.Security.Claims;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class AuthService
    {
        private readonly IRepository<User> _users;
        public AuthService(IRepository<User> users) => _users = users;

        public async Task<User?> ValidateAsync(string email, string password)
        {
            var u = (await _users.GetAllAsync()).FirstOrDefault(x => x.Email == email);
            if (u == null) return null;

            var hash = u.PasswordHash ?? "";

            // Nhận diện bcrypt ($2a$/$2b$/$2y$). Nếu chưa phải bcrypt, coi như plain → so sánh trực tiếp và NÂNG CẤP lên bcrypt.
            bool isBcrypt = hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$");

            bool ok = isBcrypt
                ? BCrypt.Net.BCrypt.Verify(password, hash)               // đúng cách cho bcrypt
                : password == hash;                                      // tạm hỗ trợ legacy plain

            if (!ok) return null;

            // AUTO-MIGRATE: nếu trước giờ lưu plain, sau lần login thành công thì hash lại để an toàn
            if (!isBcrypt)
            {
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _users.UpsertAsync(x => x.Id == u.Id, u);
            }

            return u;
        }

        public static ClaimsPrincipal CreatePrincipal(User u)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, u.Id),
                new(ClaimTypes.Name, u.Name),
                new(ClaimTypes.Email, u.Email),
                new(ClaimTypes.Role, u.Role.ToString()),
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }

        public async Task<bool> ChangePasswordAsync(string userId, string oldPass, string newPass)
        {
            var u = await _users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u == null) return false;
            if (!BCrypt.Net.BCrypt.Verify(oldPass, u.PasswordHash)) return false;

            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
            await _users.UpsertAsync(x => x.Id == u.Id, u);
            return true;
        }
    }
}

