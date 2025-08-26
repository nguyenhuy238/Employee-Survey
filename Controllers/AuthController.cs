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
        private readonly PasswordResetService _reset;

        public AuthController(AuthService auth, PasswordResetService reset)
        {
            _auth = auth;
            _reset = reset;
        }

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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // ĐÃ THÊM CASE Manager
            return u.Role switch
            {
                Role.Admin => RedirectToAction("Dashboard", "Admin"),
                Role.HR => RedirectToAction("Dashboard", "HR"),
                Role.Manager => RedirectToAction("Dashboard", "Manager"),
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

        // ======= Forgot/Reset password flow giữ nguyên như trước =======

        [HttpGet("/auth/forgot")]
        public IActionResult Forgot() => View();

        [HttpPost("/auth/forgot")]
        public async Task<IActionResult> ForgotPost(string email)
        {
            await _reset.RequestAsync(email);
            TempData["Info"] = "Nếu email tồn tại, mã OTP đã được gửi.";
            return RedirectToAction("Verify", new { email });
        }

        [HttpGet("/auth/verify")]
        public IActionResult Verify(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpPost("/auth/verify")]
        public async Task<IActionResult> VerifyPost(string email, string otp)
        {
            var token = await _reset.VerifyOtpAsync(email, otp);
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Email = email;
                ViewBag.Error = "OTP không hợp lệ hoặc đã hết hạn.";
                return View("Verify");
            }
            return RedirectToAction("Reset", new { token });
        }

        [HttpGet("/auth/reset")]
        public IActionResult Reset(string token)
        {
            ViewBag.Token = token;
            return View();
        }

        [HttpPost("/auth/reset")]
        public async Task<IActionResult> ResetPost(string token, string password, string confirm)
        {
            if (string.IsNullOrWhiteSpace(password) || password != confirm)
            {
                ViewBag.Token = token;
                ViewBag.Error = "Mật khẩu không khớp.";
                return View("Reset");
            }

            var ok = await _reset.ResetPasswordAsync(token, password);
            if (!ok)
            {
                ViewBag.Token = token;
                ViewBag.Error = "Liên kết đặt lại không hợp lệ hoặc đã hết hạn.";
                return View("Reset");
            }

            TempData["Info"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }
    }
}
