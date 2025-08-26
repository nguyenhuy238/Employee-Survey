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

        private int ComputeRemainingSeconds(Session s, Test t)
        {
            var durationMinutes = Math.Max(1, t.DurationMinutes);
            var total = durationMinutes * 60;

            var runningDelta = s.TimerStartedAt.HasValue
                ? (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds)
                : 0;

            var consumed = Math.Max(0, s.ConsumedSeconds + runningDelta);
            return Math.Max(0, total - consumed);
        }

        [HttpGet("/mytests/session/{id}")]
        public async Task<IActionResult> Runner(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            var t = s is null ? null : await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            if (s == null || t == null) return NotFound();

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            // đánh dấu hoạt động
            s.LastActivityAt = DateTime.UtcNow;

            // Khi vào trang Runner -> nếu đang pause thì resume để bắt đầu tính giờ
            if (!s.TimerStartedAt.HasValue)
                s.TimerStartedAt = DateTime.UtcNow;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);

            var remainingSeconds = ComputeRemainingSeconds(s, t);

            ViewBag.TestTitle = t.Title;
            ViewBag.Duration = t.DurationMinutes;               // compat
            ViewBag.RemainingSeconds = remainingSeconds;

            return View(s);
        }

        [HttpGet("/mytests/result/{id}")]
        public async Task<IActionResult> Result(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            // dọn session nháp khác
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
