using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class MyTestsController : Controller
    {
        private readonly AssignmentService _assignSvc;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Session> _sRepo;

        public MyTestsController(AssignmentService a, IRepository<Test> t, IRepository<Session> s)
        { _assignSvc = a; _tRepo = t; _sRepo = s; }

        public async Task<IActionResult> Index()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.UtcNow;

            // Tests được assign còn hạn
            var testIds = await _assignSvc.GetAvailableTestIdsAsync(uid, now);
            var allTests = await _tRepo.GetAllAsync();

            // Mọi session của user
            var sessions = (await _sRepo.GetAllAsync())
                .Where(x => x.UserId == uid)
                .ToList();

            // 1) Available = assigned - đã start
            var startedTestIds = sessions.Select(s => s.TestId).ToHashSet();
            var tests = allTests
                .Where(x => testIds.Contains(x.Id) && !startedTestIds.Contains(x.Id))
                .ToList();

            // Helper chọn thời điểm mới nhất
            static DateTime Key(Session s)
                => (s.LastActivityAt == default ? s.StartAt : s.LastActivityAt);

            // Các test đã có kết quả
            var submittedTestIds = sessions
                .Where(x => x.Status != SessionStatus.Draft)
                .Select(x => x.TestId)
                .ToHashSet();

            // 2) In Progress: chỉ 1 phiên mới nhất cho mỗi Test, và loại bỏ test đã có kết quả
            var inProgress = sessions
                .Where(x => x.Status == SessionStatus.Draft && !submittedTestIds.Contains(x.TestId))
                .GroupBy(x => x.TestId)
                .Select(g => g.OrderByDescending(Key).First())
                .OrderByDescending(Key)
                .ToList();

            // 3) Submitted: mới nhất lên trước
            var submitted = sessions
                .Where(x => x.Status != SessionStatus.Draft)
                .OrderByDescending(x => x.EndAt ?? x.StartAt)
                .ToList();

            ViewBag.InProgress = inProgress;
            ViewBag.Submitted = submitted;

            // (tuỳ chọn) cung cấp dict tiêu đề test cho view nếu cần hiển thị đẹp
            ViewBag.TestTitles = allTests.ToDictionary(t => t.Id, t => t.Title);

            return View(tests);
        }
    }
}
