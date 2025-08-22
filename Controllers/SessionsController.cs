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
            // Chỉ chủ sở hữu session mới được truy cập
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();
            ViewBag.Duration = t.DurationMinutes;
            return View(s);
        }

        [HttpGet("/mytests/result/{id}")]
        public async Task<IActionResult> Result(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            // Chỉ chủ sở hữu session mới được xem kết quả
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();
            return View(s);
        }
    }
}
