namespace Employee_Survey.Infrastructure
{
    public class EmailOptions
    {
        public string Host { get; set; } = "";     // smtp.gmail.com ...
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = "";     // tài khoản SMTP
        public string Pass { get; set; } = "";     // mật khẩu / app password
        public string From { get; set; } = "";     // From hiển thị
        public string FromName { get; set; } = "Employee Survey";
    }
}
