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
            // Chỉ cho phép start nếu test đang được assign cho user tại thời điểm hiện tại
            var allowed = await _assignSvc.GetAvailableTestIdsAsync(uid, DateTime.UtcNow);
            if (!allowed.Contains(id)) return Forbid();

            var s = await _svc.StartAsync(id, uid);
            // Trả về đề (không có đáp án đúng)
            var q = s.Snapshot.Select(x => new { x.Id, x.Type, x.Content, x.Options });
            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == id);
            var duration = test?.DurationMinutes ?? 30;
            return Ok(new { sessionId = s.Id, questions = q, durationMinutes = duration });
        }

        public class SubmitPayload { public Dictionary<string, string?> Answers { get; set; } = new(); }

        [HttpPost("sessions/{sid}/submit")]
        public async Task<IActionResult> Submit(string sid, [FromBody] SubmitPayload p)
        {
            // Xác thực quyền sở hữu session
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s0 = await _sRepo.FirstOrDefaultAsync(x => x.Id == sid);
            if (s0 == null) return NotFound();
            if (!string.Equals(s0.UserId, uid, StringComparison.Ordinal)) return Forbid();

            var s = await _svc.SubmitAsync(sid, p.Answers);
            return Ok(new { s.Id, s.TotalScore, s.Status });
        }
    }
}
