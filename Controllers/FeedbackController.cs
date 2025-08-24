// Employee_Survey.Controllers/FeedbackController.cs
using System.Security.Claims;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class FeedbackController : Controller
    {
        private readonly IRepository<Feedback> _fRepo;
        private readonly IRepository<Session> _sRepo;
        private readonly IRepository<Test> _tRepo;

        public FeedbackController(
            IRepository<Feedback> fRepo,
            IRepository<Session> sRepo,
            IRepository<Test> tRepo)
        {
            _fRepo = fRepo;
            _sRepo = sRepo;
            _tRepo = tRepo;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET /Feedback/Create?sessionId=...
        [HttpGet]
        public async Task<IActionResult> Create(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("Thiếu sessionId.");

            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == sessionId);
            if (s == null) return NotFound("Session không tồn tại.");
            if (s.UserId != CurrentUserId) return Forbid();
            if (s.Status == SessionStatus.Draft)
                return BadRequest("Bạn cần nộp bài trước khi gửi feedback.");

            // Nếu đã có feedback cho session -> chuyển sang Edit
            var existed = await _fRepo.FirstOrDefaultAsync(x => x.SessionId == sessionId);
            if (existed != null) return RedirectToAction(nameof(Edit), new { id = existed.Id });

            var test = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            var vm = new FeedbackCreateViewModel
            {
                SessionId = sessionId,
                TestTitle = test?.Title ?? s.TestId
            };
            return View(vm);
        }

        // POST /Feedback/Create
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Create(FeedbackCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == vm.SessionId);
            if (s == null) return NotFound("Session không tồn tại.");
            if (s.UserId != CurrentUserId) return Forbid();
            if (s.Status == SessionStatus.Draft)
            {
                ModelState.AddModelError("", "Bạn cần nộp bài trước khi gửi feedback.");
                return View(vm);
            }

            // Đảm bảo duy nhất
            var existed = await _fRepo.FirstOrDefaultAsync(x => x.SessionId == vm.SessionId);
            if (existed != null) return RedirectToAction(nameof(Edit), new { id = existed.Id });

            var f = new Feedback
            {
                SessionId = vm.SessionId,
                Content = vm.Content.Trim(),
                Rating = vm.Rating,
                CreatedAt = DateTime.UtcNow
            };

            await _fRepo.InsertAsync(f);
            TempData["Msg"] = "Cảm ơn bạn đã gửi feedback!";
            return RedirectToAction("Result", "Sessions", new { id = vm.SessionId });
        }

        // GET /Feedback/Edit/{id}
        [HttpGet("/Feedback/Edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var f = await _fRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();

            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == f.SessionId);
            if (s == null) return NotFound("Session không tồn tại.");
            if (s.UserId != CurrentUserId) return Forbid();

            var test = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            var vm = new FeedbackCreateViewModel
            {
                SessionId = f.SessionId,
                Content = f.Content,
                Rating = f.Rating,
                TestTitle = test?.Title ?? s.TestId
            };
            ViewBag.FeedbackId = id;
            return View(vm);
        }

        // POST /Feedback/Edit/{id}
        [ValidateAntiForgeryToken]
        [HttpPost("/Feedback/Edit/{id}")]
        public async Task<IActionResult> Edit(string id, FeedbackCreateViewModel vm)
        {
            if (!ModelState.IsValid) { ViewBag.FeedbackId = id; return View(vm); }

            var f = await _fRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();

            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == f.SessionId);
            if (s == null) return NotFound("Session không tồn tại.");
            if (s.UserId != CurrentUserId) return Forbid();

            f.Content = vm.Content.Trim();
            f.Rating = vm.Rating;
            // không đổi CreatedAt; nếu muốn, có thể thêm UpdatedAt

            await _fRepo.UpsertAsync(x => x.Id == f.Id, f);
            TempData["Msg"] = "Đã cập nhật feedback.";
            return RedirectToAction("Result", "Sessions", new { id = f.SessionId });
        }
    }
}
