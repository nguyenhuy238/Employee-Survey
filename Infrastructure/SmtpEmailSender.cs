using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Employee_Survey.Infrastructure
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        public SmtpEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            using var client = new SmtpClient(_opt.Host, _opt.Port)
            {
                Credentials = new NetworkCredential(_opt.User, _opt.Pass),
                EnableSsl = _opt.EnableSsl
            };

            using var msg = new MailMessage()
            {
                From = new MailAddress(_opt.From, _opt.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            msg.To.Add(toEmail);

            await client.SendMailAsync(msg);
        }
    }
}
