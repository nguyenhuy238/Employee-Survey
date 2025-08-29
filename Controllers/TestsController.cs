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
using System.Text;
using System.Text.RegularExpressions;

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

        // ---------- Index ----------
        public async Task<IActionResult> Index(string? status = null)
        {
            var list = await _repo.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
                    list = list.Where(t => !t.IsPublished).ToList();
                else if (string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase))
                    list = list.Where(t => t.IsPublished).ToList();
            }

            return View(list);
        }

        // ---------- Create (GET) ----------
        [HttpGet]
        public async Task<IActionResult> Create([FromQuery] QuestionFilter f)
        {
            if (f.Page <= 0) f.Page = 1;

            // Hỗ trợ ?PageSize=all
            var psRawQS = HttpContext.Request.Query["PageSize"].ToString();
            if (string.Equals(psRawQS, "all", StringComparison.OrdinalIgnoreCase))
                f.PageSize = int.MaxValue;
            else if (f.PageSize <= 0)
                f.PageSize = 20;

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

            // NEW: giữ giá trị form khi paging (đọc từ query)
            string q(string key) => HttpContext.Request.Query[key].ToString();
            if (!string.IsNullOrWhiteSpace(q("Title"))) model.Title = q("Title");
            if (int.TryParse(q("DurationMinutes"), out var dur)) model.DurationMinutes = dur;
            if (int.TryParse(q("PassScore"), out var pass)) model.PassScore = pass;
            if (!string.IsNullOrWhiteSpace(q("SkillFilter"))) model.SkillFilter = q("SkillFilter");
            if (int.TryParse(q("RandomMCQ"), out var r1)) model.RandomMCQ = r1;
            if (int.TryParse(q("RandomTF"), out var r2)) model.RandomTF = r2;
            if (int.TryParse(q("RandomEssay"), out var r3)) model.RandomEssay = r3;

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
                var psRaw = HttpContext.Request.Query["PageSize"].ToString();
                if (string.Equals(psRaw, "all", StringComparison.OrdinalIgnoreCase))
                    f.PageSize = int.MaxValue;

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

        // ---------- Edit (GET) ----------
        [HttpGet]
        public async Task<IActionResult> Edit(string id, [FromQuery] QuestionFilter f)
        {
            var t = await _repo.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            if (f.Page <= 0) f.Page = 1;

            // Hỗ trợ ?PageSize=all
            var psRawQS = HttpContext.Request.Query["PageSize"].ToString();
            if (string.Equals(psRawQS, "all", StringComparison.OrdinalIgnoreCase))
                f.PageSize = int.MaxValue;
            else if (f.PageSize <= 0)
                f.PageSize = 20;

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

            // NEW: giữ giá trị form khi paging
            string q(string key) => HttpContext.Request.Query[key].ToString();
            if (!string.IsNullOrWhiteSpace(q("Title"))) vm.Title = q("Title");
            if (int.TryParse(q("DurationMinutes"), out var dur)) vm.DurationMinutes = dur;
            if (int.TryParse(q("PassScore"), out var pass)) vm.PassScore = pass;
            if (bool.TryParse(q("ShuffleQuestions"), out var sh)) vm.ShuffleQuestions = sh;
            if (!string.IsNullOrWhiteSpace(q("SkillFilter"))) vm.SkillFilter = q("SkillFilter");
            if (int.TryParse(q("RandomMCQ"), out var r1)) vm.RandomMCQ = r1;
            if (int.TryParse(q("RandomTF"), out var r2)) vm.RandomTF = r2;
            if (int.TryParse(q("RandomEssay"), out var r3)) vm.RandomEssay = r3;

            return View(vm);
        }

        // ---------- Edit (POST) ----------
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
                var psRaw = HttpContext.Request.Query["PageSize"].ToString();
                if (string.Equals(psRaw, "all", StringComparison.OrdinalIgnoreCase))
                    f.PageSize = int.MaxValue;

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

        // ---------- Assign (GET) ----------
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
        [HttpPost("Tests/Assign/{id?}")] // chấp nhận cả dạng có {id} để tránh 405
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(
            [FromRoute] string? id,                 // không dùng, chỉ để match route nếu có
            [FromForm] string testId,
            [FromForm] List<string>? userIds,
            [FromForm] DateTime? startAt,
            [FromForm] DateTime? endAt)
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

            // clear cũ
            await _aRepo.DeleteAsync(a => a.TestId == testId && a.TargetType == "User");

            var newAssigned = (userIds ?? new List<string>()).Distinct().ToList();

            // nếu không tick ai: coi như lưu rỗng và thoát
            if (newAssigned.Count == 0)
            {
                TempData["Msg"] = "Đã lưu: không có user nào được assign.";
                return RedirectToAction(nameof(Assign), new { id = testId });
            }

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

            // notify user mới
            var newlyAdded = newAssigned.Where(uid => !oldAssigned.Contains(uid)).ToList();
            if (newlyAdded.Count > 0)
            {
                var allUsers = await _uRepo.GetAllAsync();
                var targets = allUsers
                    .Where(u => newlyAdded.Contains(u.Id))
                    .Select(u => new AssignmentNotifyTarget { User = u, SessionId = string.Empty })
                    .ToList();

                if (targets.Count > 0)
                    await NotifySafe(test!, targets, s, e);
            }

            TempData["Msg"] = "Đã lưu danh sách assign và publish test" +
                              (_notiService != null ? " (đã gửi thông báo/email cho user mới)." : ".");
            return RedirectToAction(nameof(Assign), new { id = testId });
        }

        // ---------- AssignByDepartment ----------
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

        // ---------- BulkAssign ----------
        [HttpPost("/Tests/BulkAssign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssign([FromForm] List<string> testIds, string userId, DateTime? startAt, DateTime? endAt)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Err"] = "Thiếu UserId.";
                return RedirectToAction(nameof(Index));
            }
            if (testIds == null || testIds.Count == 0)
            {
                TempData["Err"] = "Bạn chưa chọn Test nào.";
                return RedirectToAction(nameof(Index));
            }

            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            int assigned = 0;
            foreach (var tid in testIds.Distinct())
            {
                var t = await _repo.FirstOrDefaultAsync(x => x.Id == tid);
                if (t == null) continue;

                if (!t.IsPublished)
                {
                    t.IsPublished = true;
                    t.PublishedAt = DateTime.UtcNow;
                    await _repo.UpsertAsync(x => x.Id == t.Id, t);
                }

                await _aRepo.DeleteAsync(a => a.TestId == tid && a.TargetType == "User" && a.TargetValue == userId);

                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = tid,
                    TargetType = "User",
                    TargetValue = userId,
                    StartAt = s,
                    EndAt = e
                });
                assigned++;
            }

            TempData["Msg"] = $"Đã assign {assigned} test cho user '{userId}'.";
            return RedirectToAction(nameof(Index), new { status = "Draft" });
        }

        // ---------- BulkAssignAuto ----------
        [HttpPost("/Tests/BulkAssignAuto")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssignAuto([FromForm] List<string> testIds, DateTime? startAt, DateTime? endAt)
        {
            if (testIds == null || testIds.Count == 0)
            {
                TempData["Err"] = "Bạn chưa chọn Test nào.";
                return RedirectToAction(nameof(Index));
            }

            var users = await _uRepo.GetAllAsync();
            var tests = await _repo.GetAllAsync();

            var testMap = tests.ToDictionary(t => t.Id, t => t);
            var s = startAt ?? DateTime.UtcNow.AddDays(-1);
            var e = endAt ?? DateTime.UtcNow.AddDays(30);

            int assigned = 0;
            var skipped = new List<string>();

            foreach (var tid in testIds.Distinct())
            {
                if (!testMap.TryGetValue(tid, out var t)) { skipped.Add($"{tid} (not found)"); continue; }

                var owner = ResolveOwnerUser(users, t);
                if (owner == null) { skipped.Add(t.Title); continue; }

                if (!t.IsPublished)
                {
                    t.IsPublished = true;
                    t.PublishedAt = DateTime.UtcNow;
                    await _repo.UpsertAsync(x => x.Id == t.Id, t);
                }

                await _aRepo.DeleteAsync(a => a.TestId == tid && a.TargetType == "User" && a.TargetValue == owner.Id);

                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = tid,
                    TargetType = "User",
                    TargetValue = owner.Id,
                    StartAt = s,
                    EndAt = e
                });
                assigned++;
            }

            var msg = $"Đã assign {assigned} test (auto by owner).";
            if (skipped.Count > 0)
                msg += $" Bỏ qua {skipped.Count}: {string.Join("; ", skipped.Take(5))}{(skipped.Count > 5 ? "..." : "")}";
            TempData["Msg"] = msg;

            return RedirectToAction(nameof(Index), new { status = "Draft" });
        }

        // ---------- Helpers ----------
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
            catch { /* log nếu có */ }
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private User? ResolveOwnerUser(List<User> users, Test t)
        {
            var title = t.Title ?? "";

            // 1) uid: <id>
            var mUid = Regex.Match(title, @"uid\s*:\s*(?<id>[A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
            if (mUid.Success)
            {
                var id = mUid.Groups["id"].Value.Trim();
                var byId = users.FirstOrDefault(u => string.Equals(u.Id, id, StringComparison.Ordinal));
                if (byId != null) return byId;
            }

            // 2) Auto - <FullName> - ...
            var mName = Regex.Match(title, @"^Auto\s*-\s*(?<name>[^-]+?)\s*-", RegexOptions.IgnoreCase);
            if (mName.Success)
            {
                var nameInTitle = mName.Groups["name"].Value.Trim();
                var normTitleName = RemoveDiacritics(nameInTitle).ToLowerInvariant();

                var byName = users.FirstOrDefault(u =>
                    RemoveDiacritics(u.Name ?? "").ToLowerInvariant() == normTitleName);
                if (byName != null) return byName;

                var byEmailLocal = users.FirstOrDefault(u =>
                {
                    var local = (u.Email ?? "").Split('@')[0];
                    return RemoveDiacritics(local).ToLowerInvariant() == normTitleName.Replace(" ", "");
                });
                if (byEmailLocal != null) return byEmailLocal;
            }

            return null;
        }
    }
}
