using Employee_Survey.Application;
using Employee_Survey.Domain;
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
        public TestsApiController(TestService svc) => _svc = svc;

        [HttpPost("{id}/start")]
        public async Task<IActionResult> Start(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var s = await _svc.StartAsync(id, uid);
            // Trả về đề (không có đáp án đúng)
            var q = s.Snapshot.Select(x => new { x.Id, x.Type, x.Content, x.Options });
            return Ok(new { sessionId = s.Id, questions = q, durationMinutes = 30 }); // duration tạm
        }

        public class SubmitPayload { public Dictionary<string, string?> Answers { get; set; } = new(); }

        [HttpPost("sessions/{sid}/submit")]
        public async Task<IActionResult> Submit(string sid, [FromBody] SubmitPayload p)
        {
            var s = await _svc.SubmitAsync(sid, p.Answers);
            return Ok(new { s.Id, s.TotalScore, s.Status });
        }
    }
}
