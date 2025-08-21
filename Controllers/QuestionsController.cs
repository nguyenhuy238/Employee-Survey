using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR")]
    public class QuestionsController : Controller
    {
        private readonly IRepository<Question> _repo;
        public QuestionsController(IRepository<Question> repo) => _repo = repo;

        public async Task<IActionResult> Index()
        {
            var list = await _repo.GetAllAsync();
            return View(list.OrderBy(x => x.Skill).ToList());
        }

        [HttpGet]
        public IActionResult Create() => View(new Question { Type = QType.MCQ, Options = new() { "A", "B", "C", "D" } });

        [HttpPost]
        public async Task<IActionResult> Create(Question q)
        {
            if (q.Type == QType.MCQ && (q.Options == null || q.Options.Count == 0)) ModelState.AddModelError("", "MCQ cần Options");
            if (!ModelState.IsValid) return View(q);
            await _repo.InsertAsync(q);
            return RedirectToAction(nameof(Index));
        }
    }
}
