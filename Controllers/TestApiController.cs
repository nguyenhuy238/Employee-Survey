using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Employee_Survey.Controllers
{
    [ApiController]
    [Route("api/tests")]
    [Authorize]
    public class TestsApiController : ControllerBase
    {
        private readonly TestService _svc;
        private readonly AssignmentService _assignSvc;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Session> _sRepo;

        public TestsApiController(TestService svc, AssignmentService assignSvc, IRepository<Test> tRepo, IRepository<Session> sRepo)
        { _svc = svc; _assignSvc = assignSvc; _tRepo = tRepo; _sRepo = sRepo; }

        // ====================== START ======================
        [HttpPost("{id}/start")]
        public async Task<IActionResult> Start(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var allowed = await _assignSvc.GetAvailableTestIdsAsync(uid, DateTime.UtcNow);
            if (!allowed.Contains(id)) return Forbid();

            // Tạo mới hoặc trả về session đang làm (service của bạn)
            var s = await _svc.StartAsync(id, uid);

            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == id);
            var duration = test?.DurationMinutes ?? 30;

            var remainingSeconds = ComputeRemainingSeconds(s, duration);

            var q = s.Snapshot.Select(x => new { x.Id, x.Type, x.Content, x.Options });

            return Ok(new
            {
                sessionId = s.Id,
                questions = q,
                durationMinutes = duration,
                remainingMinutes = (int)Math.Ceiling(remainingSeconds / 60.0)
            });
        }

        // ====================== SUBMIT ======================
        public class SubmitPayload { public Dictionary<string, string?> Answers { get; set; } = new(); }

        [HttpPost("sessions/{sid}/submit")]
        public async Task<IActionResult> Submit(string sid, [FromBody] SubmitPayload p)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s0 = await _sRepo.FirstOrDefaultAsync(x => x.Id == sid);
            if (s0 == null) return NotFound();
            if (!string.Equals(s0.UserId, uid, StringComparison.Ordinal)) return Forbid();

            // CHO PHÉP nộp để hỗ trợ auto-submit phía client.
            var s = await _svc.SubmitAsync(sid, p.Answers);

            return Ok(new
            {
                s.Id,
                s.TotalScore,
                s.MaxScore,
                s.Percent,
                s.IsPassed,
                s.Status
            });
        }

        // ====================== PAUSE ======================
        [HttpPost("sessions/{sid}/pause")]
        public async Task<IActionResult> Pause(string sid)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == sid);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            if (t == null) return NotFound();

            // Gộp thời gian đang chạy vào ConsumedSeconds và clear TimerStartedAt
            if (s.TimerStartedAt.HasValue)
            {
                var delta = (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds);
                s.ConsumedSeconds = Math.Max(0, s.ConsumedSeconds + Math.Max(0, delta));
                s.TimerStartedAt = null;
                s.LastActivityAt = DateTime.UtcNow;
                await _sRepo.UpsertAsync(x => x.Id == s.Id, s);
            }

            var remaining = ComputeRemainingSeconds(s, t.DurationMinutes);
            return Ok(new { remainingSeconds = remaining, running = false });
        }

        // ====================== RESUME ======================
        [HttpPost("sessions/{sid}/resume")]
        public async Task<IActionResult> Resume(string sid)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == sid);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            if (t == null) return NotFound();

            // Hết giờ hoặc đã nộp/đã chấm => không resume
            var remaining = ComputeRemainingSeconds(s, t.DurationMinutes);
            if (remaining <= 0 || s.Status != SessionStatus.Draft)
                return Ok(new { remainingSeconds = Math.Max(0, remaining), running = false });

            // Bắt đầu chạy tiếp nếu đang không chạy
            if (!s.TimerStartedAt.HasValue)
            {
                s.TimerStartedAt = DateTime.UtcNow;
                s.LastActivityAt = DateTime.UtcNow;
                await _sRepo.UpsertAsync(x => x.Id == s.Id, s);
            }

            remaining = ComputeRemainingSeconds(s, t.DurationMinutes);
            return Ok(new { remainingSeconds = remaining, running = true });
        }

        // ====================== Helpers ======================
        private static int ComputeRemainingSeconds(Session s, int durationMinutes)
        {
            var total = Math.Max(1, durationMinutes) * 60;

            var runningDelta = s.TimerStartedAt.HasValue
                ? (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds)
                : 0;

            var consumed = Math.Max(0, s.ConsumedSeconds + Math.Max(0, runningDelta));
            return Math.Max(0, total - consumed);
        }
    }
}
