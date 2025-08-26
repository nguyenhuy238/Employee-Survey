using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        // Bạn có thể inject các service/report cần cho Manager ở đây
        private readonly IRepository<Test> _testRepo;
        private readonly IRepository<User> _userRepo;

        public ManagerController(IRepository<Test> testRepo, IRepository<User> userRepo)
        {
            _testRepo = testRepo;
            _userRepo = userRepo;
        }

        // GET: /Manager/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            // Demo data cơ bản cho dashboard quản lý
            var tests = await _testRepo.GetAllAsync();
            var employees = (await _userRepo.GetAllAsync()).Where(x => x.Role == Role.Employee).ToList();

            ViewBag.TotalTests = tests.Count;
            ViewBag.TotalEmployees = employees.Count;

            return View();
        }
    }
}
