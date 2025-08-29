using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR,Manager")]
    [Route("autotests")]
    public class AutoTestsController : Controller
    {
        private readonly ITestGenerationService _svc;
        private readonly IRepository<User> _uRepo;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Assignment> _aRepo;
        private readonly INotificationService? _noti;

        public AutoTestsController(
            ITestGenerationService svc,
            IRepository<User> uRepo,
            IRepository<Test> tRepo,
            IRepository<Assignment> aRepo,
            INotificationService? noti = null)
        { _svc = svc; _uRepo = uRepo; _tRepo = tRepo; _aRepo = aRepo; _noti = noti; }

        [HttpGet("generate")]
        public async Task<IActionResult> Generate()
        {
            var depts = (await _uRepo.GetAllAsync())
                .Select(u => u.Department ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            ViewBag.Departments = depts;

            return View(new AutoTestOptions
            {
                Mode = "Department",
                DifficultyPolicy = "ByLevel",
                McqCount = 8,
                TfCount = 2,
                MatchingCount = 0,
                DragDropCount = 0,
                EssayCount = 0,
                TotalScore = 10m
            });
        }

        // POST: Generate cá nhân hoá -> hiện trang BatchResult preview
        [HttpPost("generate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate([FromForm] AutoTestOptions opt)
        {
            var actor = User.Identity?.Name ?? "admin";
            try
            {
                var results = await _svc.GeneratePersonalizedAsync(opt, actor);
                // Gắn cửa sổ thời gian assign để dùng cho Assign All
                ViewBag.StartAtUtc = opt.StartAtUtc ?? DateTime.UtcNow.AddDays(-1);
                ViewBag.EndAtUtc = opt.EndAtUtc ?? DateTime.UtcNow.AddDays(30);
                return View("BatchResult", results);
            }
            catch (Exception ex)
            {
                TempData["Err"] = ex.Message;
                return RedirectToAction(nameof(Generate));
            }
        }

        // Assign một test cho 1 user
        [HttpPost("assign-one")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignOne(string testId, string userId, DateTime? startAt, DateTime? endAt)
        {
            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == testId);
            if (t == null) return NotFound();

            if (!t.IsPublished) { t.IsPublished = true; t.PublishedAt = DateTime.UtcNow; await _tRepo.UpsertAsync(x => x.Id == t.Id, t); }

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            await _aRepo.InsertAsync(new Assignment
            {
                TestId = testId,
                TargetType = "User",
                TargetValue = userId,
                StartAt = s,
                EndAt = e
            });

            TempData["Msg"] = "Đã assign test.";
            return RedirectToAction(nameof(Generate));
        }

        // Assign tất cả test hiển thị trong BatchResult
        [HttpPost("assign-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAll([FromForm] string[] testIds, [FromForm] string[] userIds, DateTime? startAt, DateTime? endAt)
        {
            if (testIds.Length != userIds.Length) return BadRequest();

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            for (int i = 0; i < testIds.Length; i++)
            {
                var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == testIds[i]);
                if (t == null) continue;

                if (!t.IsPublished) { t.IsPublished = true; t.PublishedAt = DateTime.UtcNow; await _tRepo.UpsertAsync(x => x.Id == t.Id, t); }

                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = testIds[i],
                    TargetType = "User",
                    TargetValue = userIds[i],
                    StartAt = s,
                    EndAt = e
                });
            }

            TempData["Msg"] = $"Đã assign {testIds.Length} đề cho người dùng tương ứng.";
            return RedirectToAction(nameof(Generate));
        }
    }
}
