using System.Security.Claims;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers.Api
{
    [ApiController]
    [Authorize]
    [Route("api/tests/sessions")]
    public class SessionsApiController : ControllerBase
    {
        private readonly IRepository<Session> _sRepo;
        private readonly IRepository<Test> _tRepo;

        public SessionsApiController(IRepository<Session> sRepo, IRepository<Test> tRepo)
        {
            _sRepo = sRepo; _tRepo = tRepo;
        }

        // Helper: tính remaining giây theo Session + Test
        private async Task<int> GetRemainingSecondsAsync(Session s)
        {
            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            var duration = Math.Max(1, t?.DurationMinutes ?? 30);
            var total = duration * 60;

            var runningDelta = s.TimerStartedAt.HasValue
                ? (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds)
                : 0;

            var consumed = Math.Max(0, s.ConsumedSeconds + runningDelta);
            return Math.Max(0, total - consumed);
        }

        // POST /api/tests/sessions/{id}/touch (giữ nguyên)
        [HttpPost("{id}/touch")]
        public async Task<IActionResult> Touch(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            s.LastActivityAt = DateTime.UtcNow;
            await _sRepo.UpsertAsync(x => x.Id == id, s);
            var remaining = await GetRemainingSecondsAsync(s);
            return Ok(new { ok = true, at = s.LastActivityAt, remainingSeconds = remaining, running = s.TimerStartedAt.HasValue });
        }

        // ===== NEW: RESUME =====
        [HttpPost("{id}/resume")]
        public async Task<IActionResult> Resume(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            if (!s.TimerStartedAt.HasValue)
            {
                s.TimerStartedAt = DateTime.UtcNow;
            }
            s.LastActivityAt = DateTime.UtcNow;

            await _sRepo.UpsertAsync(x => x.Id == id, s);
            var remaining = await GetRemainingSecondsAsync(s);
            return Ok(new { ok = true, remainingSeconds = remaining, running = true });
        }

        // ===== NEW: PAUSE =====
        [HttpPost("{id}/pause")]
        public async Task<IActionResult> Pause(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            if (s.TimerStartedAt.HasValue)
            {
                var delta = (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds);
                if (delta > 0) s.ConsumedSeconds += delta;
                s.TimerStartedAt = null;
            }
            s.LastActivityAt = DateTime.UtcNow;

            await _sRepo.UpsertAsync(x => x.Id == id, s);
            var remaining = await GetRemainingSecondsAsync(s);
            return Ok(new { ok = true, remainingSeconds = remaining, running = false });
        }
    }
}
