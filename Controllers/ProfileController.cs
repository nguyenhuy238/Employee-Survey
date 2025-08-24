using System.Security.Claims;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IRepository<User> _userRepo;
        private readonly AuthService _authService;

        public ProfileController(IRepository<User> userRepo, AuthService authService)
        {
            _userRepo = userRepo;
            _authService = authService;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet("/profile")]
        public async Task<IActionResult> Edit()
        {
            var uid = CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login", "Auth");

            var u = await _userRepo.FirstOrDefaultAsync(x => x.Id == uid);
            if (u == null) return NotFound();

            var vm = new ProfileViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Level = u.Level,
                TeamId = u.TeamId ?? "",
                RoleName = u.Role.ToString()
            };
            return View(vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("/profile")]
        public async Task<IActionResult> Edit(ProfileViewModel vm)
        {
            var uid = CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login", "Auth");
            if (!ModelState.IsValid) return View(vm);

            var u = await _userRepo.FirstOrDefaultAsync(x => x.Id == uid);
            if (u == null) return NotFound();

            // Kiểm tra trùng email (ngoại trừ chính mình)
            var all = await _userRepo.GetAllAsync();
            var emailExists = all.Any(x => x.Email.Equals(vm.Email, StringComparison.OrdinalIgnoreCase)
                                           && x.Id != u.Id);
            if (emailExists)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email đã được sử dụng bởi tài khoản khác.");
                return View(vm);
            }

            // Cập nhật các trường cho phép người dùng tự sửa
            u.Name = vm.Name?.Trim() ?? "";
            u.Email = vm.Email?.Trim() ?? "";
            u.Level = vm.Level?.Trim() ?? "Junior";
            u.TeamId = vm.TeamId?.Trim() ?? "";

            await _userRepo.UpsertAsync(x => x.Id == u.Id, u);

            // Cập nhật lại ClaimsPrincipal để phản ánh Name/Email mới ngay
            await HttpContext.SignOutAsync("cookie");
            await HttpContext.SignInAsync("cookie", AuthService.CreatePrincipal(u));

            TempData["Success"] = "Đã cập nhật hồ sơ thành công.";
            return RedirectToAction(nameof(Edit));
        }

        [HttpGet("/profile/change-password")]
        public IActionResult ChangePassword()
        {
            var uid = CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login", "Auth");

            return View(new ChangePasswordViewModel { UserId = uid });
        }

        [ValidateAntiForgeryToken]
        [HttpPost("/profile/change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            var uid = CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid) return View(vm);

            var ok = await _authService.ChangePasswordAsync(uid, vm.OldPassword, vm.NewPassword);
            if (!ok)
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không đúng hoặc tài khoản không tồn tại.");
                return View(vm);
            }

            TempData["Success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Edit));
        }
    }
}
