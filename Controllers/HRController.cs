using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee_Survey.Application;
using Employee_Survey.Infrastructure;
using Employee_Survey.Domain;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly ReportService _report;
        private readonly IRepository<Test> _testRepo;
        private readonly IRepository<User> _userRepo;
        
        public HRController(ReportService report, IRepository<Test> testRepo, IRepository<User> userRepo) 
        { 
            _report = report; 
            _testRepo = testRepo;
            _userRepo = userRepo;
        }

        // /HR/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _report.GetHrDashboardAsync(DateTime.UtcNow);
            
            // Truyền data cho dropdown
            ViewBag.AvailableTests = await _testRepo.GetAllAsync();
            ViewBag.AvailableUsers = (await _userRepo.GetAllAsync()).Where(u => u.Role == Role.Employee).ToList();
            
            return View(vm);
        }

        // (Tùy chọn) /HR/ExportRecent
        [HttpGet]
        public async Task<IActionResult> ExportRecent()
        {
            var (name, csv) = await _report.ExportRecentSubmissionsCsvAsync();
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", name);
        }
    }
}
