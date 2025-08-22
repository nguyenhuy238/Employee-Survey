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
            return RedirectToAction(nameof(Index));
        }

        // --- Gán nhanh MVP ---
        [HttpPost]
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
            return RedirectToAction("Dashboard", "HR");
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
            return RedirectToAction("Dashboard", "HR");
        }
    }
}




//using Employee_Survey.Application;
//using Employee_Survey.Domain;
//using Employee_Survey.Infrastructure;
//using Employee_Survey.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//namespace Employee_Survey.Controllers
//{       
//    [Authorize(Roles = "Admin,HR")]
//    public class TestsController : Controller
//    {
//        private readonly IRepository<Test> _repo;
//        private readonly IRepository<Assignment> _aRepo;
//        private readonly IRepository<User> _uRepo;
//        private readonly IQuestionService _questionService; // Thêm vào biến này

//        public TestsController(IRepository<Test> r, IRepository<Assignment> aRepo, IRepository<User> uRepo, IQuestionService questionService)
//        { 
//            _repo = r; 
//            _aRepo = aRepo; 
//            _uRepo = uRepo; 
//            _questionService = questionService; // Khởi tạo biến này
//        }

//        public async Task<IActionResult> Index() => View(await _repo.GetAllAsync());

//        [HttpGet]
//        public async Task<IActionResult> Create()
//        {
//            // Use SearchAsync with an empty filter to get all questions
//            var pagedResult = await _questionService.SearchAsync(new QuestionFilter());
//            var questions = pagedResult.Items;

//            var model = new CreateTestViewModel
//            {
//                MCQQuestions = questions.Where(q => q.Type == QType.MCQ).ToList(),
//                TFQuestions = questions.Where(q => q.Type == QType.TrueFalse).ToList(),
//                EssayQuestions = questions.Where(q => q.Type == QType.Essay).ToList(),
//                // ... other fields
//            };
//            return View(model);
//        }

//        [HttpPost]
//        public async Task<IActionResult> Create(Test t)
//        {
//            if (t.RandomMCQ + t.RandomTF + t.RandomEssay <= 0)
//                ModelState.AddModelError("", "Số câu phải > 0");
//            if (!ModelState.IsValid) return View(t);

//            // Tự động publish test khi tạo
//            t.IsPublished = true;
//            t.CreatedAt = DateTime.UtcNow;
//            t.PublishedAt = DateTime.UtcNow;

//            await _repo.InsertAsync(t);
//            return RedirectToAction(nameof(Index));
//        }

//        // Gán nhanh (MVP): gán cho user id cụ thể
//        [HttpPost]
//        public async Task<IActionResult> AssignToUser(string testId, string userId)
//        {
//            var test = await _repo.FirstOrDefaultAsync(t => t.Id == testId);
//            if (test != null && !test.IsPublished)
//            {
//                test.IsPublished = true;
//                test.PublishedAt = DateTime.UtcNow;
//                await _repo.UpsertAsync(t => t.Id == testId, test);
//            }

//            await _aRepo.InsertAsync(new Assignment { 
//                TestId = testId, 
//                TargetType = "User", 
//                TargetValue = userId,
//                StartAt = DateTime.UtcNow.AddDays(-1),
//                EndAt = DateTime.UtcNow.AddDays(30)
//            });

//            // Redirect về HR Dashboard với thông báo thành công
//            TempData["Msg"] = $"Đã assign test '{test?.Title ?? testId}' cho user '{userId}' thành công!";
//            return RedirectToAction("Dashboard", "HR");
//        }

//        // Hiển thị danh sách User để chọn và assign
//        [HttpGet]
//        public async Task<IActionResult> Assign(string id)
//        {
//            var test = await _repo.FirstOrDefaultAsync(t => t.Id == id);
//            if (test == null) return NotFound();

//            var users = (await _uRepo.GetAllAsync()).Where(u => u.Role == Role.Employee).ToList();
//            var assigns = (await _aRepo.GetAllAsync())
//                .Where(a => a.TestId == id && a.TargetType == "User")
//                .Select(a => a.TargetValue)
//                .ToHashSet();

//            var vm = new AssignUsersViewModel
//            {
//                TestId = id,
//                TestTitle = test.Title,
//                Users = users,
//                AssignedUserIds = assigns
//            };
//            return View(vm);
//        }

//        // Lưu danh sách user được assign (thay thế toàn bộ assign theo User cho test)
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Assign(string testId, List<string> userIds, DateTime? startAt, DateTime? endAt)
//        {
//            var test = await _repo.FirstOrDefaultAsync(t => t.Id == testId);
//            if (test == null) return NotFound();

//            // Đảm bảo test được publish trước khi assign
//            if (!test.IsPublished)
//            {
//                test.IsPublished = true;
//                test.PublishedAt = DateTime.UtcNow;
//                await _repo.UpsertAsync(t => t.Id == testId, test);
//            }

//            // Xóa các assign theo User hiện có cho test này
//            await _aRepo.DeleteAsync(a => a.TestId == testId && a.TargetType == "User");

//            var s = startAt ?? DateTime.UtcNow.AddDays(-1); // Bắt đầu từ hôm qua
//            var e = endAt ?? DateTime.UtcNow.AddDays(30);   // Kết thúc sau 30 ngày

//            // Thêm lại từ lựa chọn mới
//            if (userIds != null)
//            {
//                foreach (var uid in userIds.Distinct())
//                {
//                    await _aRepo.InsertAsync(new Assignment
//                    {
//                        TestId = testId,
//                        TargetType = "User",
//                        TargetValue = uid,
//                        StartAt = s,
//                        EndAt = e
//                    });
//                }
//            }

//            TempData["Msg"] = "Đã lưu danh sách assign và publish test";
//            return RedirectToAction("Dashboard", "HR");
//        }
//    }
//}



