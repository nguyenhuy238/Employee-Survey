using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class MyTestsController : Controller
    {
        private readonly AssignmentService _assignSvc;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Session> _sRepo;

        public MyTestsController(AssignmentService a, IRepository<Test> t, IRepository<Session> s)
        { _assignSvc = a; _tRepo = t; _sRepo = s; }

        public async Task<IActionResult> Index()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var testIds = await _assignSvc.GetAvailableTestIdsAsync(uid, DateTime.UtcNow);
            var tests = (await _tRepo.GetAllAsync()).Where(x => testIds.Contains(x.Id)).ToList();

            var sessions = (await _sRepo.GetAllAsync()).Where(x => x.UserId == uid).ToList();
            ViewBag.InProgress = sessions.Where(x => x.Status == SessionStatus.Draft).ToList();
            ViewBag.Submitted = sessions.Where(x => x.Status != SessionStatus.Draft).ToList();
            return View(tests);
        }
    }
}
