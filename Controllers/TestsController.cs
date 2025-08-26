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

        public TestsController(
            IRepository<Test> r,
            IRepository<Assignment> aRepo,
            IRepository<User> uRepo,
            IQuestionService questionService)
        {
            _repo = r;
            _aRepo = aRepo;
            _uRepo = uRepo;
            _questionService = questionService;
        }

        public async Task<IActionResult> Index() => View(await _repo.GetAllAsync());

        // ---------- Create (GET) có phân trang ----------
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

        // ---------- Assign nhanh ----------
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

            await _aRepo.InsertAsync(new Assignment
            {
                TestId = testId,
                TargetType = "User",
                TargetValue = userId,
                StartAt = DateTime.UtcNow.AddDays(-1),
                EndAt = DateTime.UtcNow.AddDays(30)
            });

            TempData["Msg"] = $"Đã assign test '{test?.Title ?? testId}' cho user '{userId}' thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Assign (GET) + lọc Department ----------
        [HttpGet]
        public async Task<IActionResult> Assign(string id, string? department = null)
        {
            var test = await _repo.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            var allUsers = (await _uRepo.GetAllAsync())
                .Where(u => u.Role == Role.Employee)
                .ToList();

            // Danh sách Department (distinct, có thể có rỗng)
            var departments = allUsers
                .Select(u => u.Department ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            // Lọc theo Department nếu có chọn
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

        // ---------- Assign (POST) tick từng user ----------
        [HttpPost]
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

            // Xoá assign User cũ rồi lưu lại theo danh sách mới
            await _aRepo.DeleteAsync(a => a.TestId == testId && a.TargetType == "User");

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            if (userIds != null)
            {
                foreach (var uid in userIds.Distinct())
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
            }

            TempData["Msg"] = "Đã lưu danh sách assign và publish test";
            return RedirectToAction(nameof(Assign), new { id = testId });
        }

        // ---------- NEW: Assign toàn bộ Department ----------
        [HttpPost]
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

            // Xoá các assign User cũ để tránh trùng, sau đó gán lại theo department
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

            TempData["Msg"] = $"Đã assign {selectedUsers.Count} user của Department '{department}' vào test và publish test.";
            return RedirectToAction(nameof(Assign), new { id = testId, department });
        }
    }
}
