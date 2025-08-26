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

        [HttpPost("{id}/start")]
        public async Task<IActionResult> Start(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var allowed = await _assignSvc.GetAvailableTestIdsAsync(uid, DateTime.UtcNow);
            if (!allowed.Contains(id)) return Forbid();

            var s = await _svc.StartAsync(id, uid); // tạo mới hoặc trả session đang làm

            var q = s.Snapshot.Select(x => new { x.Id, x.Type, x.Content, x.Options });
            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == id);
            var duration = test?.DurationMinutes ?? 30;

            var elapsed = DateTime.UtcNow - s.StartAt;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            var totalSeconds = duration * 60;
            var elapsedSeconds = (int)Math.Floor(elapsed.TotalSeconds);
            var remainingSeconds = Math.Max(0, totalSeconds - elapsedSeconds);
            var remainingMinutes = (int)Math.Ceiling(remainingSeconds / 60.0);

            return Ok(new
            {
                sessionId = s.Id,
                questions = q,
                durationMinutes = duration,
                remainingMinutes
            });
        }

        public class SubmitPayload { public Dictionary<string, string?> Answers { get; set; } = new(); }

        [HttpPost("sessions/{sid}/submit")]
        public async Task<IActionResult> Submit(string sid, [FromBody] SubmitPayload p)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s0 = await _sRepo.FirstOrDefaultAsync(x => x.Id == sid);
            if (s0 == null) return NotFound();
            if (!string.Equals(s0.UserId, uid, StringComparison.Ordinal)) return Forbid();

            // Trước đây có chặn quá giờ; nay CHO PHÉP nộp để hỗ trợ auto-submit client.
            // (Nếu muốn chấm như đã trả lời đến thời điểm hết giờ, vẫn dùng payload hiện tại.)

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
    }
}
