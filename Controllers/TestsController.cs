using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR")]
    public class TestsController : Controller
    {
        private readonly IRepository<Test> _repo;
        private readonly IRepository<Assignment> _aRepo;
        private readonly IRepository<User> _uRepo;
        private readonly IQuestionService _questionService;
        private readonly INotificationService? _notiService;

        public TestsController(
            IRepository<Test> r,
            IRepository<Assignment> aRepo,
            IRepository<User> uRepo,
            IQuestionService questionService,
            INotificationService? notiService = null)
        {
            _repo = r;
            _aRepo = aRepo;
            _uRepo = uRepo;
            _questionService = questionService;
            _notiService = notiService;
        }

        public async Task<IActionResult> Index() => View(await _repo.GetAllAsync());

        // ---------- Create (GET) ----------
        [HttpGet]
        public async Task<IActionResult> Create([FromQuery] QuestionFilter f)
        {
            if (f.Page <= 0) f.Page = 1;
            if (f.PageSize <= 0) f.PageSize = 20;

            var paged = await _questionService.SearchAsync(f);
            var model = new CreateTestViewModel
            {
                Filter = f,
                Page = paged,
                Title = "",
                DurationMinutes = 10,
                PassScore = 3,
                SkillFilter = "ASP.NET",
                RandomMCQ = 2,
                RandomTF = 1,
                RandomEssay = 0
            };
            return View(model);
        }

        // ---------- Create (POST) ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Test t, [FromForm] List<string>? SelectedQuestionIds)
        {
            if (SelectedQuestionIds != null && SelectedQuestionIds.Any())
            {
                t.QuestionIds = SelectedQuestionIds.Distinct().ToList();
                t.RandomMCQ = 0; t.RandomTF = 0; t.RandomEssay = 0;
            }
            else
            {
                if (t.RandomMCQ + t.RandomTF + t.RandomEssay <= 0)
                    ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 câu hỏi hoặc cấu hình số lượng random > 0");
            }

            if (!ModelState.IsValid)
            {
                var f = new QuestionFilter
                {
                    Page = int.TryParse(HttpContext.Request.Query["Page"], out var p) ? p : 1,
                    PageSize = int.TryParse(HttpContext.Request.Query["PageSize"], out var ps) ? ps : 20,
                    Keyword = HttpContext.Request.Query["Keyword"],
                    Skill = HttpContext.Request.Query["Skill"],
                    Difficulty = HttpContext.Request.Query["Difficulty"],
                    TagsCsv = HttpContext.Request.Query["TagsCsv"],
                    Sort = HttpContext.Request.Query["Sort"]
                };

                var paged = await _questionService.SearchAsync(f);

                var vm = new CreateTestViewModel
                {
                    Filter = f,
                    Page = paged,
                    Title = t.Title,
                    DurationMinutes = t.DurationMinutes,
                    PassScore = t.PassScore,
                    SkillFilter = t.SkillFilter,
                    RandomMCQ = t.RandomMCQ,
                    RandomTF = t.RandomTF,
                    RandomEssay = t.RandomEssay,
                    SelectedQuestionIds = SelectedQuestionIds ?? new List<string>()
                };
                return View(vm);
            }

            t.IsPublished = true;
            t.CreatedAt = DateTime.UtcNow;
            t.PublishedAt = DateTime.UtcNow;
            t.FrozenRandom = new FrozenRandomConfig
            {
                SkillFilter = t.SkillFilter,
                RandomMCQ = t.RandomMCQ,
                RandomTF = t.RandomTF,
                RandomEssay = t.RandomEssay
            };

            await _repo.InsertAsync(t);
            TempData["Msg"] = "Đã tạo và publish bài test.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Edit ----------
        [HttpGet]
        public async Task<IActionResult> Edit(string id, [FromQuery] QuestionFilter f)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            if (f.Page <= 0) f.Page = 1;
            if (f.PageSize <= 0) f.PageSize = 20;

            var paged = await _questionService.SearchAsync(f);

            var vm = new EditTestViewModel
            {
                Id = t.Id,
                Title = t.Title,
                DurationMinutes = t.DurationMinutes,
                PassScore = t.PassScore,
                ShuffleQuestions = t.ShuffleQuestions,
                SkillFilter = t.SkillFilter,
                RandomMCQ = t.RandomMCQ,
                RandomTF = t.RandomTF,
                RandomEssay = t.RandomEssay,
                IsPublished = t.IsPublished,
                Filter = f,
                Page = paged,
                SelectedQuestionIds = (t.QuestionIds ?? new()).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditTestViewModel vm, [FromForm] List<string>? SelectedQuestionIds)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (t == null) return NotFound();

            if ((SelectedQuestionIds == null || !SelectedQuestionIds.Any()) &&
                (vm.RandomMCQ + vm.RandomTF + vm.RandomEssay <= 0))
            {
                ModelState.AddModelError("", "Chọn ít nhất 1 câu hỏi hoặc cấu hình random > 0.");
            }

            if (!ModelState.IsValid)
            {
                var f = new QuestionFilter
                {
                    Page = int.TryParse(HttpContext.Request.Query["Page"], out var p) ? p : 1,
                    PageSize = int.TryParse(HttpContext.Request.Query["PageSize"], out var ps) ? ps : 20,
                    Keyword = HttpContext.Request.Query["Keyword"],
                    Skill = HttpContext.Request.Query["Skill"],
                    Difficulty = HttpContext.Request.Query["Difficulty"],
                    TagsCsv = HttpContext.Request.Query["TagsCsv"],
                    Sort = HttpContext.Request.Query["Sort"]
                };

                vm.Filter = f;
                vm.Page = await _questionService.SearchAsync(f);
                vm.SelectedQuestionIds = SelectedQuestionIds ?? new List<string>();
                return View(vm);
            }

            t.Title = vm.Title;
            t.DurationMinutes = vm.DurationMinutes;
            t.PassScore = vm.PassScore;
            t.ShuffleQuestions = vm.ShuffleQuestions;
            t.SkillFilter = vm.SkillFilter;
            t.RandomMCQ = vm.RandomMCQ;
            t.RandomTF = vm.RandomTF;
            t.RandomEssay = vm.RandomEssay;
            t.UpdatedAt = DateTime.UtcNow;

            if (SelectedQuestionIds != null && SelectedQuestionIds.Any())
            {
                t.QuestionIds = SelectedQuestionIds.Distinct().ToList();
                t.RandomMCQ = 0; t.RandomTF = 0; t.RandomEssay = 0;
            }
            else
            {
                t.QuestionIds = new();
            }

            await _repo.UpsertAsync(x => x.Id == t.Id, t);
            TempData["Msg"] = "Đã lưu thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Delete ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            await _aRepo.DeleteAsync(a => a.TestId == id);
            await _repo.DeleteAsync(t => (t as Test)!.Id == id);
            TempData["Msg"] = "Đã xoá bài test.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Toggle publish ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            t.IsPublished = !t.IsPublished;
            if (t.IsPublished)
            {
                t.PublishedAt = DateTime.UtcNow;
                t.FrozenRandom = new FrozenRandomConfig
                {
                    SkillFilter = t.SkillFilter,
                    RandomMCQ = t.RandomMCQ,
                    RandomTF = t.RandomTF,
                    RandomEssay = t.RandomEssay
                };
                TempData["Msg"] = "Đã chuyển sang Published.";
            }
            else
            {
                t.PublishedAt = null;
                TempData["Msg"] = "Đã chuyển sang Draft.";
            }

            await _repo.UpsertAsync(x => x.Id == t.Id, t);
            return RedirectToAction(nameof(Index));
        }

        // ---------- Assign nhanh (1 user) ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignToUser(string testId, string userId)
        {
            var test = await _repo.FirstOrDefaultAsync(t => t.Id == testId);
            if (test != null && !test.IsPublished)
            {
                test.IsPublished = true;
                test.PublishedAt = DateTime.UtcNow;
                await _repo.UpsertAsync(t => t.Id == testId, test);
            }

            var s = DateTime.UtcNow.AddDays(-1);
            var e = DateTime.UtcNow.AddDays(30);

            await _aRepo.InsertAsync(new Assignment
            {
                TestId = testId,
                TargetType = "User",
                TargetValue = userId,
                StartAt = s,
                EndAt = e
            });

            var u = await _uRepo.FirstOrDefaultAsync(x => x.Id == userId);
            if (test != null && u != null)
            {
                var targets = new[]
                {
                    new AssignmentNotifyTarget { User = u, SessionId = string.Empty }
                };
                await NotifySafe(test, targets, s, e);
            }

            TempData["Msg"] = $"Đã assign test '{test?.Title ?? testId}' cho user '{userId}'" +
                              (_notiService != null ? " và đã gửi thông báo/email." : ".");
            return RedirectToAction(nameof(Index));
        }

        // ---------- Assign (GET) + lọc Department ----------
        [HttpGet("Tests/Assign/{id}")]
        [HttpGet("/Tests/Assign")]
        public async Task<IActionResult> Assign(string id, [FromQuery] string? department = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var test = await _repo.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            var allUsers = (await _uRepo.GetAllAsync())
                .Where(u => u.Role == Role.Employee)
                .ToList();

            var departments = allUsers
                .Select(u => u.Department ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            var usersToShow = allUsers;
            if (!string.IsNullOrWhiteSpace(department))
            {
                usersToShow = allUsers
                    .Where(u => string.Equals(u.Department ?? "", department, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var assigns = (await _aRepo.GetAllAsync())
                .Where(a => a.TestId == id && a.TargetType == "User")
                .Select(a => a.TargetValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.Ordinal);

            var vm = new AssignUsersViewModel
            {
                TestId = id,
                TestTitle = test.Title,
                Users = usersToShow,
                AssignedUserIds = assigns,
                Departments = departments,
                SelectedDepartment = department
            };
            return View(vm);
        }

        // ---------- Assign (POST) ----------
        [HttpPost("/Tests/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string testId, List<string> userIds, DateTime? startAt, DateTime? endAt)
        {
            var test = await _repo.FirstOrDefaultAsync(t => t.Id == testId);
            if (test == null) return NotFound();

            if (!test.IsPublished)
            {
                test.IsPublished = true;
                test.PublishedAt = DateTime.UtcNow;
                await _repo.UpsertAsync(t => t.Id == testId, test);
            }

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            var oldAssigned = (await _aRepo.GetAllAsync())
                .Where(a => a.TestId == testId && a.TargetType == "User")
                .Select(a => a.TargetValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.Ordinal);

            await _aRepo.DeleteAsync(a => a.TestId == testId && a.TargetType == "User");

            var newAssigned = (userIds ?? new()).Distinct().ToList();
            foreach (var uid in newAssigned)
            {
                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = testId,
                    TargetType = "User",
                    TargetValue = uid,
                    StartAt = s,
                    EndAt = e
                });
            }

            var newlyAdded = newAssigned.Where(uid => !oldAssigned.Contains(uid)).ToList();
            if (newlyAdded.Count > 0)
            {
                var allUsers = await _uRepo.GetAllAsync();
                var targets = allUsers
                    .Where(u => newlyAdded.Contains(u.Id))
                    .Select(u => new AssignmentNotifyTarget
                    {
                        User = u,
                        SessionId = string.Empty
                    })
                    .ToList();

                if (targets.Count > 0)
                    await NotifySafe(test!, targets, s, e);
            }

            TempData["Msg"] = "Đã lưu danh sách assign và publish test" +
                              (_notiService != null ? " (đã gửi thông báo/email cho user mới)." : ".");
            return RedirectToAction(nameof(Assign), new { id = testId });
        }

        // ---------- Assign toàn bộ Department ----------
        [HttpPost("/Tests/AssignByDepartment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignByDepartment(string testId, string department, DateTime? startAt, DateTime? endAt)
        {
            var test = await _repo.FirstOrDefaultAsync(t => t.Id == testId);
            if (test == null) return NotFound();

            if (string.IsNullOrWhiteSpace(department))
            {
                TempData["Err"] = "Vui lòng chọn Department.";
                return RedirectToAction(nameof(Assign), new { id = testId });
            }

            if (!test.IsPublished)
            {
                test.IsPublished = true;
                test.PublishedAt = DateTime.UtcNow;
                await _repo.UpsertAsync(t => t.Id == testId, test);
            }

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            var allUsers = (await _uRepo.GetAllAsync())
                .Where(u => u.Role == Role.Employee)
                .ToList();

            var selectedUsers = allUsers
                .Where(u => string.Equals(u.Department ?? "", department, StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Id)
                .Distinct()
                .ToList();

            if (selectedUsers.Count == 0)
            {
                TempData["Err"] = $"Không tìm thấy user nào trong Department '{department}'.";
                return RedirectToAction(nameof(Assign), new { id = testId, department });
            }

            var oldAssigned = (await _aRepo.GetAllAsync())
                .Where(a => a.TestId == testId && a.TargetType == "User")
                .Select(a => a.TargetValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.Ordinal);

            await _aRepo.DeleteAsync(a => a.TestId == testId && a.TargetType == "User");
            foreach (var uid in selectedUsers)
            {
                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = testId,
                    TargetType = "User",
                    TargetValue = uid,
                    StartAt = s,
                    EndAt = e
                });
            }

            var newlyAdded = selectedUsers.Where(uid => !oldAssigned.Contains(uid)).ToList();
            if (newlyAdded.Count > 0)
            {
                var targets = allUsers
                    .Where(u => newlyAdded.Contains(u.Id))
                    .Select(u => new AssignmentNotifyTarget
                    {
                        User = u,
                        SessionId = string.Empty
                    })
                    .ToList();

                if (targets.Count > 0)
                    await NotifySafe(test!, targets, s, e);
            }

            TempData["Msg"] = $"Đã assign {selectedUsers.Count} user của Department '{department}' vào test và publish test" +
                              (_notiService != null ? " (đã gửi thông báo/email cho user mới)." : ".");
            return RedirectToAction(nameof(Assign), new { id = testId, department });
        }

        // ---------- Helper ----------
        private async Task NotifySafe(
            Test test,
            IEnumerable<AssignmentNotifyTarget> targets,
            DateTime startAtUtc,
            DateTime endAtUtc)
        {
            if (_notiService == null) return;
            try
            {
                await _notiService.NotifyAssignmentsAsync(test, targets, startAtUtc, endAtUtc);
            }
            catch
            {
                // TODO: log nếu có IAuditService/ILogger
            }
        }
    }
}
