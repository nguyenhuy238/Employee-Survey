using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly IRepository<Session> _sRepo;
        private readonly IRepository<Test> _tRepo;

        public SessionsController(IRepository<Session> s, IRepository<Test> t) { _sRepo = s; _tRepo = t; }

        [HttpGet("/mytests/session/{id}")]
        public async Task<IActionResult> Runner(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            var t = s is null ? null : await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            if (s == null || t == null) return NotFound();

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            // Đánh dấu hoạt động gần nhất để trang "In Progress" chọn phiên mới nhất
            s.LastActivityAt = DateTime.UtcNow;
            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);

            ViewBag.Duration = t.DurationMinutes;
            ViewBag.TestTitle = t.Title;
            return View(s);
        }

        [HttpGet("/mytests/result/{id}")]
        public async Task<IActionResult> Result(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            // Sau khi có kết quả: dọn mọi nháp khác của cùng Test cho user này
            var currentTestId = s.TestId;
            await _sRepo.DeleteAsync(x => x.UserId == uid
                                          && x.TestId == currentTestId
                                          && x.Id != s.Id
                                          && x.Status == SessionStatus.Draft);

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            ViewBag.TestTitle = t?.Title ?? "Result";
            return View(s);
        }
    }
}
