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
        public SessionsApiController(IRepository<Session> sRepo) { _sRepo = sRepo; }

        // POST /api/tests/sessions/{id}/touch
        [HttpPost("{id}/touch")]
        public async Task<IActionResult> Touch(string id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!string.Equals(s.UserId, uid, StringComparison.Ordinal)) return Forbid();

            s.LastActivityAt = DateTime.UtcNow;
            await _sRepo.UpsertAsync(x => x.Id == id, s);
            return Ok(new { ok = true, at = s.LastActivityAt });
        }
    }
}
