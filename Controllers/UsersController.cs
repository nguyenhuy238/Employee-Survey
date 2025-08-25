using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Team> _teamRepo;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IRepository<User> userRepo, IRepository<Team> teamRepo, ILogger<UsersController> logger)
        {
            _userRepo = userRepo;
            _teamRepo = teamRepo;
            _logger = logger;
        }

        // GET: /Users
        public async Task<IActionResult> Index(string? q, string? teamId, Role? role)
        {
            var users = await _userRepo.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(q))
                users = users.Where(u =>
                    (u.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            if (!string.IsNullOrWhiteSpace(teamId))
                users = users.Where(u => u.TeamId == teamId).ToList();

            if (role.HasValue)
                users = users.Where(u => u.Role == role.Value).ToList();

            ViewBag.Teams = await _teamRepo.GetAllAsync();
            ViewBag.Query = q;
            ViewBag.TeamId = teamId;
            ViewBag.Role = role;

            return View(users.OrderBy(u => u.Name).ToList());
        }

        // GET: /Users/Create
        public async Task<IActionResult> Create()
        {
            await LoadLookups();
            return View(new UserFormViewModel());
        }

        // POST: /Users/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel vm)
        {
            await LoadLookups();

            var all = await _userRepo.GetAllAsync();
            if (all.Any(u => u.Email.Equals(vm.Email, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(vm.Email), "Email đã tồn tại.");

            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Password là bắt buộc khi tạo mới.");

            if (!ModelState.IsValid) return View(vm);

            var entity = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = vm.Name.Trim(),
                Email = vm.Email.Trim(),
                Role = vm.Role,
                Level = vm.Level.Trim(),
                TeamId = vm.TeamId ?? ""
            };
            entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password!);

            await _userRepo.InsertAsync(entity);
            TempData["Msg"] = "Tạo user thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            var u = await _userRepo.FirstOrDefaultAsync(x => (x as User)!.Id == id) as User;
            if (u == null) return NotFound();

            await LoadLookups();

            var vm = new UserFormViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                Level = u.Level,
                TeamId = u.TeamId
            };
            return View(vm);
        }

        // POST: /Users/Edit/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserFormViewModel vm)
        {
            var u = await _userRepo.FirstOrDefaultAsync(x => (x as User)!.Id == id) as User;
            if (u == null) return NotFound();

            await LoadLookups();

            var all = await _userRepo.GetAllAsync();
            if (all.Any(x => x.Email.Equals(vm.Email, StringComparison.OrdinalIgnoreCase) && x.Id != id))
                ModelState.AddModelError(nameof(vm.Email), "Email đã được dùng bởi user khác.");

            if (!ModelState.IsValid) return View(vm);

            u.Name = vm.Name.Trim();
            u.Email = vm.Email.Trim();
            u.Role = vm.Role;
            u.Level = vm.Level.Trim();
            u.TeamId = vm.TeamId ?? u.TeamId;

            if (!string.IsNullOrWhiteSpace(vm.Password))
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password);

            await _userRepo.UpsertAsync(x => (x as User)!.Id == id, u);
            TempData["Msg"] = "Cập nhật user thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/Delete/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            await _userRepo.DeleteAsync(x => (x as User)!.Id == id);
            TempData["Msg"] = "Đã xóa user.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/ResetPassword/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword)
        {
            var u = await _userRepo.FirstOrDefaultAsync(x => (x as User)!.Id == id) as User;
            if (u == null) return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Err"] = "Password mới không được trống.";
                return RedirectToAction(nameof(Index));
            }

            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepo.UpsertAsync(x => (x as User)!.Id == id, u);
            TempData["Msg"] = "Đã reset mật khẩu.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadLookups()
        {
            var teams = await _teamRepo.GetAllAsync();
            ViewBag.TeamItems = new SelectList(teams, "Id", "Name");

            var levels = new List<string> { "Intern", "Junior", "Middle", "Senior", "Lead" };
            ViewBag.LevelItems = new SelectList(levels);
            // Role -> dùng Html.GetEnumSelectList<Role>() trong View
        }
    }
}
