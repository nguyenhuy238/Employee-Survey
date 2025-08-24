// Employee_Survey.Controllers/HRController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee_Survey.Application;
using Employee_Survey.Infrastructure;
using Employee_Survey.Domain;
using Employee_Survey.Models;
using System.Text;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly ReportService _report;
        private readonly IRepository<Test> _testRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Feedback> _fbRepo;
        private readonly IRepository<Session> _sRepo;

        public HRController(
            ReportService report,
            IRepository<Test> testRepo,
            IRepository<User> userRepo,
            IRepository<Feedback> fbRepo,
            IRepository<Session> sRepo)
        {
            _report = report;
            _testRepo = testRepo;
            _userRepo = userRepo;
            _fbRepo = fbRepo;
            _sRepo = sRepo;
        }

        // /HR/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _report.GetHrDashboardAsync(DateTime.UtcNow);

            // Dropdown cho Quick Assign
            ViewBag.AvailableTests = await _testRepo.GetAllAsync();
            ViewBag.AvailableUsers = (await _userRepo.GetAllAsync())
                .Where(u => u.Role == Role.Employee)
                .ToList();

            return View(vm);
        }

        // (Tùy chọn) /HR/ExportRecent
        [HttpGet]
        public async Task<IActionResult> ExportRecent()
        {
            var (name, csv) = await _report.ExportRecentSubmissionsCsvAsync();
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", name);
        }

        // /HR/Feedbacks?testId=...  → Xem feedback (có lọc theo Test)
        [HttpGet("/HR/Feedbacks")]
        public async Task<IActionResult> Feedbacks(string? testId = null)
        {
            var tests = await _testRepo.GetAllAsync();
            var fbs = await _fbRepo.GetAllAsync();
            var sessions = await _sRepo.GetAllAsync();
            var users = await _userRepo.GetAllAsync();

            // Map & lọc theo testId nếu có
            var query = fbs.Select(f =>
            {
                var s = sessions.FirstOrDefault(x => x.Id == f.SessionId);
                var u = users.FirstOrDefault(x => x.Id == s?.UserId);
                var t = tests.FirstOrDefault(x => x.Id == s?.TestId);

                return new HrFeedbackItemVm
                {
                    FeedbackId = f.Id,
                    SessionId = f.SessionId,
                    UserName = u?.Name ?? s?.UserId ?? "(unknown)",
                    UserEmail = u?.Email ?? "",
                    TestTitle = t?.Title ?? s?.TestId ?? "(unknown)",
                    CreatedAt = f.CreatedAt,
                    Rating = f.Rating,
                    Content = f.Content
                };
            });

            if (!string.IsNullOrWhiteSpace(testId))
            {
                // Lọc bằng TestId (so với Session.TestId), khớp theo TestTitle đã map
                // Vì ta đã mất TestId khi map sang VM, lọc ngay trên sessions trước sẽ chính xác hơn:
                var sessionIdsOfTest = sessions
                    .Where(s => s.TestId == testId)
                    .Select(s => s.Id)
                    .ToHashSet();

                query = query.Where(vm => sessionIdsOfTest.Contains(vm.SessionId));
            }

            var list = query
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            // ViewBags cho form lọc
            ViewBag.Tests = tests
                .OrderBy(t => t.Title)
                .Select(t => new { t.Id, t.Title })
                .ToList();
            ViewBag.SelectedTestId = testId ?? "";

            return View(list);
        }

        // /HR/ExportFeedbacksCsv?testId=...
        [HttpGet("/HR/ExportFeedbacksCsv")]
        public async Task<IActionResult> ExportFeedbacksCsv(string? testId = null)
        {
            var tests = await _testRepo.GetAllAsync();
            var fbs = await _fbRepo.GetAllAsync();
            var sessions = await _sRepo.GetAllAsync();
            var users = await _userRepo.GetAllAsync();

            var rows = new List<string>();
            // Header
            rows.Add("CreatedAt,TestTitle,UserName,UserEmail,Rating,Content,SessionId");

            // Xác định tập session theo test nếu có
            HashSet<string>? sessionIdsOfTest = null;
            string fileName = "feedbacks_all.csv";
            if (!string.IsNullOrWhiteSpace(testId))
            {
                sessionIdsOfTest = sessions.Where(s => s.TestId == testId).Select(s => s.Id).ToHashSet();
                var tTitle = tests.FirstOrDefault(t => t.Id == testId)?.Title ?? testId;
                fileName = $"feedbacks_{SanitizeFileName(tTitle)}.csv";
            }

            foreach (var f in fbs.OrderByDescending(x => x.CreatedAt))
            {
                if (sessionIdsOfTest != null && !sessionIdsOfTest.Contains(f.SessionId))
                    continue;

                var s = sessions.FirstOrDefault(x => x.Id == f.SessionId);
                var t = tests.FirstOrDefault(x => x.Id == s?.TestId);
                var u = users.FirstOrDefault(x => x.Id == s?.UserId);

                var created = f.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var testTitle = t?.Title ?? s?.TestId ?? "";
                var userName = u?.Name ?? s?.UserId ?? "";
                var userEmail = u?.Email ?? "";
                var rating = f.Rating.ToString();
                var content = CsvEscape(f.Content);
                var ssid = f.SessionId;

                rows.Add($"{CsvEscape(created)},{CsvEscape(testTitle)},{CsvEscape(userName)},{CsvEscape(userEmail)},{rating},{content},{CsvEscape(ssid)}");
            }

            var csv = string.Join("\r\n", rows);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);

            static string CsvEscape(string? s)
            {
                s ??= "";
                if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                return s;
            }

            static string SanitizeFileName(string name)
            {
                foreach (var c in Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '_');
                return name.Replace(' ', '_').ToLowerInvariant();
            }
        }
    }
}
