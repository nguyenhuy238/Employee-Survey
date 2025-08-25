using System;

namespace Employee_Survey.Domain
{
    public class PasswordReset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string UserId { get; set; } = "";      // Liên kết User
        public string Email { get; set; } = "";       // Lưu lại email lúc yêu cầu
        public string OtpCode { get; set; } = "";     // 6 chữ số
        public DateTime ExpiresAt { get; set; }       // Hết hạn (UTC)
            = DateTime.UtcNow.AddMinutes(10);
        public bool Used { get; set; } = false;       // Đã dùng xong hay chưa
        public string ResetToken { get; set; } = "";  // Token ngẫu nhiên để mở form đặt mật khẩu
        public int Attempts { get; set; } = 0;        // Đếm lần nhập sai OTP
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
