using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR")]
    public class TestsController : Controller
    {
        private readonly IRepository<Test> _repo;
        private readonly IRepository<Assignment> _aRepo;

        public TestsController(IRepository<Test> r, IRepository<Assignment> aRepo)
        { _repo = r; _aRepo = aRepo; }

        public async Task<IActionResult> Index() => View(await _repo.GetAllAsync());

        [HttpGet]
        public IActionResult Create() => View(new Test());

        [HttpPost]
        public async Task<IActionResult> Create(Test t)
        {
            if (t.RandomMCQ + t.RandomTF + t.RandomEssay <= 0)
                ModelState.AddModelError("", "Số câu phải > 0");
            if (!ModelState.IsValid) return View(t);
            await _repo.InsertAsync(t);
            return RedirectToAction(nameof(Index));
        }

        // Gán nhanh (MVP): gán cho user id cụ thể
        [HttpPost]
        public async Task<IActionResult> AssignToUser(string testId, string userId)
        {
            await _aRepo.InsertAsync(new Assignment { TestId = testId, TargetType = "User", TargetValue = userId });
            return RedirectToAction(nameof(Index));
        }
    }
}
