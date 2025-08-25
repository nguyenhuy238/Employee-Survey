using System.Threading.Tasks;

namespace Employee_Survey.Infrastructure
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}
