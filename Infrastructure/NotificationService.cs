using System.Text;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Employee_Survey.Infrastructure
{
    public class NotificationService : INotificationService
    {
        private readonly IEmailSender _email;
        private readonly string _configuredBaseUrl;
        private readonly IHttpContextAccessor? _http;

        public NotificationService(
            IEmailSender email,
            IOptions<AppOptions> appOptions,
            IHttpContextAccessor? http = null)
        {
            _email = email;
            _http = http;
            _configuredBaseUrl = NormalizeBaseUrl(appOptions?.Value?.BaseUrl);
        }

        public async Task NotifyAssignmentsAsync(
            Test test,
            IEnumerable<AssignmentNotifyTarget> targets,
            DateTime startAtUtc,
            DateTime endAtUtc)
        {
            if (targets == null) return;

            foreach (var t in targets)
            {
                var u = t.User;
                if (u == null || string.IsNullOrWhiteSpace(u.Email)) continue;

                var relativePath = string.IsNullOrWhiteSpace(t.SessionId)
                    ? "/mytests"
                    : $"/mytests/session/{t.SessionId}";

                var absoluteUrl = ToAbsoluteUrl(relativePath);

                var subject = $"[Employee Survey] Bạn được assign test: {test.Title}";
                var body = new StringBuilder()
                    .Append("<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px'>")
                    .Append($"<p>Xin chào <b>{System.Net.WebUtility.HtmlEncode(u.Name)}</b>,</p>")
                    .Append($"<p>Bạn vừa được <b>assign</b> vào bài test <b>{System.Net.WebUtility.HtmlEncode(test.Title)}</b>.</p>")
                    .Append("<ul>")
                    .Append($"<li>Hiệu lực: <b>{startAtUtc:u}</b> → <b>{endAtUtc:u}</b> (UTC)</li>")
                    .Append($"<li>Thời lượng: <b>{test.DurationMinutes}</b> phút</li>")
                    .Append("</ul>")
                    .Append($"<p>Nhấn để vào làm: <a href='{absoluteUrl}'>{absoluteUrl}</a></p>")
                    .Append("<p>Trân trọng!</p>")
                    .Append("</div>")
                    .ToString();

                await _email.SendAsync(u.Email, subject, body);
            }
        }

        // ===== Helpers =====
        private static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return "";
            return baseUrl.Trim().TrimEnd('/');
        }

        private string ResolveBaseUrl()
        {
            if (!string.IsNullOrEmpty(_configuredBaseUrl)) return _configuredBaseUrl;

            var req = _http?.HttpContext?.Request;
            if (req == null) return "";

            var pathBase = req.PathBase.HasValue ? req.PathBase.Value : "";
            return $"{req.Scheme}://{req.Host}{pathBase}".TrimEnd('/');
        }

        private string ToAbsoluteUrl(string relativePath)
        {
            var baseUrl = ResolveBaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                return relativePath; // fallback (khuyến nghị cấu hình BaseUrl)
            }
            var path = string.IsNullOrWhiteSpace(relativePath) ? "" : relativePath.Trim();
            if (!path.StartsWith("/")) path = "/" + path;
            return $"{baseUrl}{path}";
        }
    }
}
