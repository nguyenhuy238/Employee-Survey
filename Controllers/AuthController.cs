using Employee_Survey.Application;
using Employee_Survey.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    public class AuthController : Controller
    {
        private readonly AuthService _auth;
        public AuthController(AuthService auth) => _auth = auth;

        [HttpGet("/auth/login")]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost("/auth/login")]
        public async Task<IActionResult> LoginPost(string email, string password, string? returnUrl = null)
        {
            var u = await _auth.ValidateAsync(email, password);
            if (u == null)
            {
                ViewBag.Error = "Sai email hoặc mật khẩu";
                ViewBag.ReturnUrl = returnUrl;
                return View("Login");
            }

            await HttpContext.SignInAsync("cookie", AuthService.CreatePrincipal(u));

            // Nếu có ReturnUrl hợp lệ -> quay lại đó
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Điều hướng theo Role
            return u.Role switch
            {
                Role.Admin => RedirectToAction("Dashboard", "Admin"),
                Role.HR => RedirectToAction("Dashboard", "HR"),
                Role.Employee => RedirectToAction("MySurveys", "Survey"),
                _ => RedirectToAction("MySurveys", "Survey")
            };
        }

        [Authorize, HttpPost("/auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("cookie");
            return RedirectToAction("Login");
        }

        [HttpGet("/auth/denied")]
        public IActionResult Denied() => Content("Bạn không có quyền truy cập.");
    }
}

