using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee_Survey.Application;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly ReportService _report;
        public HRController(ReportService report) => _report = report;

        // /HR/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _report.GetHrDashboardAsync(DateTime.UtcNow);
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
