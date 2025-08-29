using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR")] // đổi lại "Admin" nếu bạn muốn
    public class AdminController : Controller
    {
        private readonly IServiceProvider _sp;
        public AdminController(IServiceProvider sp) => _sp = sp;

        [HttpGet]
        public IActionResult Dashboard() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetData()
        {
            await Seeder.ResetAllJsonFilesAsync(_sp, reseed: true);
            TempData["Msg"] = "Đã reset TOÀN BỘ dữ liệu (Assignment, AuditLog, Feedback, Notification, PasswordReset, Question, Session, Team, Test, User, ...).";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
