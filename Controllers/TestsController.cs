// File: Controllers/TestsController.cs
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var pagedResult = await _questionService.SearchAsync(new QuestionFilter());
            var questions = pagedResult.Items;

            var model = new CreateTestViewModel
            {
                MCQQuestions = questions.Where(q => q.Type == QType.MCQ).ToList(),
                TFQuestions = questions.Where(q => q.Type == QType.TrueFalse).ToList(),
                EssayQuestions = questions.Where(q => q.Type == QType.Essay).ToList()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Test t, [FromForm] List<string>? SelectedQuestionIds)
        {
            // Nếu có chọn thủ công thì ưu tiên QuestionIds và tắt random
            if (SelectedQuestionIds != null && SelectedQuestionIds.Any())
            {
                t.QuestionIds = SelectedQuestionIds.Distinct().ToList();
                t.RandomMCQ = 0;
                t.RandomTF = 0;
                t.RandomEssay = 0;
            }
            else
            {
                // Không chọn thủ công -> yêu cầu random > 0
                if (t.RandomMCQ + t.RandomTF + t.RandomEssay <= 0)
                    ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 câu hỏi hoặc cấu hình số lượng random > 0");
            }

            if (!ModelState.IsValid)
            {
                // Nạp lại dữ liệu cho view
                var paged = await _questionService.SearchAsync(new QuestionFilter());
                var qs = paged.Items;
                var vm = new CreateTestViewModel
                {
                    Title = t.Title,
                    DurationMinutes = t.DurationMinutes,
                    PassScore = t.PassScore,
                    SkillFilter = t.SkillFilter,
                    RandomMCQ = t.RandomMCQ,
                    RandomTF = t.RandomTF,
                    RandomEssay = t.RandomEssay,
                    SelectedQuestionIds = SelectedQuestionIds ?? new List<string>(),
                    MCQQuestions = qs.Where(q => q.Type == QType.MCQ).ToList(),
                    TFQuestions = qs.Where(q => q.Type == QType.TrueFalse).ToList(),
                    EssayQuestions = qs.Where(q => q.Type == QType.Essay).ToList()
                };
                return View(vm);
            }

            // Tự động publish
            t.IsPublished = true;
            t.CreatedAt = DateTime.UtcNow;
            t.PublishedAt = DateTime.UtcNow;

            // Audit cấu hình random tại thời điểm publish
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

        // --- Edit ---
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            var paged = await _questionService.SearchAsync(new QuestionFilter());
            var qs = paged.Items;

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
                SelectedQuestionIds = (t.QuestionIds ?? new()).ToList(),
                MCQQuestions = qs.Where(q => q.Type == QType.MCQ).ToList(),
                TFQuestions = qs.Where(q => q.Type == QType.TrueFalse).ToList(),
                EssayQuestions = qs.Where(q => q.Type == QType.Essay).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditTestViewModel vm, [FromForm] List<string>? SelectedQuestionIds)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (t == null) return NotFound();

            // validate cơ bản
            if ((SelectedQuestionIds == null || !SelectedQuestionIds.Any()) &&
                (vm.RandomMCQ + vm.RandomTF + vm.RandomEssay <= 0))
            {
                ModelState.AddModelError("", "Chọn ít nhất 1 câu hỏi hoặc cấu hình random > 0.");
            }

            if (!ModelState.IsValid)
            {
                var paged = await _questionService.SearchAsync(new QuestionFilter());
                var qs = paged.Items;

                // nạp lại danh sách câu hỏi
                vm.MCQQuestions = qs.Where(q => q.Type == QType.MCQ).ToList();
                vm.TFQuestions = qs.Where(q => q.Type == QType.TrueFalse).ToList();
                vm.EssayQuestions = qs.Where(q => q.Type == QType.Essay).ToList();
                vm.SelectedQuestionIds = SelectedQuestionIds ?? new List<string>();
                return View(vm);
            }

            // cập nhật các trường
            t.Title = vm.Title;
            t.DurationMinutes = vm.DurationMinutes;
            t.PassScore = vm.PassScore;
            t.ShuffleQuestions = vm.ShuffleQuestions;
            t.SkillFilter = vm.SkillFilter;
            t.RandomMCQ = vm.RandomMCQ;
            t.RandomTF = vm.RandomTF;
            t.RandomEssay = vm.RandomEssay;
            t.UpdatedAt = DateTime.UtcNow;

            // nếu chọn thủ công -> set QuestionIds & tắt random
            if (SelectedQuestionIds != null && SelectedQuestionIds.Any())
            {
                t.QuestionIds = SelectedQuestionIds.Distinct().ToList();
                t.RandomMCQ = 0; t.RandomTF = 0; t.RandomEssay = 0;
            }
            else
            {
                // không chọn thủ công -> để random theo cấu hình
                t.QuestionIds = new();
            }

            await _repo.UpsertAsync(x => x.Id == t.Id, t);
            TempData["Msg"] = "Đã lưu thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        // --- Delete ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            // Xoá assignments liên quan (nếu có)
            await _aRepo.DeleteAsync(a => a.TestId == id);

            await _repo.DeleteAsync(t => (t as Test)!.Id == id);
            TempData["Msg"] = "Đã xoá bài test.";
            return RedirectToAction(nameof(Index));
        }

        // --- Toggle publish/draft ---
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

        // --- Gán nhanh MVP ---
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

        // Danh sách user để chọn assign
        [HttpGet]
        public async Task<IActionResult> Assign(string id)
        {
            var test = await _repo.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            var users = (await _uRepo.GetAllAsync()).Where(u => u.Role == Role.Employee).ToList();
            var assigns = (await _aRepo.GetAllAsync())
                .Where(a => a.TestId == id && a.TargetType == "User")
                .Select(a => a.TargetValue)
                .ToHashSet();

            var vm = new AssignUsersViewModel
            {
                TestId = id,
                TestTitle = test.Title,
                Users = users,
                AssignedUserIds = assigns
            };
            return View(vm);
        }

        // Lưu danh sách assign theo User
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
            return RedirectToAction(nameof(Index));
        }
    }
}
