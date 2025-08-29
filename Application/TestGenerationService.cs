using System;
using System.Collections.Generic;
using System.Linq;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class TestGenerationService : ITestGenerationService
    {
        private readonly IRepository<Question> _qRepo;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<User> _uRepo;
        private readonly IRepository<Assignment> _aRepo;

        public TestGenerationService(
            IRepository<Question> qRepo,
            IRepository<Test> tRepo,
            IRepository<User> uRepo,
            IRepository<Assignment> aRepo)
        {
            _qRepo = qRepo; _tRepo = tRepo; _uRepo = uRepo; _aRepo = aRepo;
        }

        // === Generate 1 đề chung cho nhóm (API cũ) ===
        public async System.Threading.Tasks.Task<(Test test, List<TestItem> items, List<User> targets)> GenerateAsync(
            AutoTestOptions opt, string actorUserName)
        {
            var allUsers = await _uRepo.GetAllAsync();
            var targets = ResolveTargets(allUsers, opt);
            if (targets.Count == 0) throw new InvalidOperationException("Không có người dùng mục tiêu.");

            var skills = (opt.Skills?.Any() == true)
                ? opt.Skills!
                : targets.GroupBy(u => (u.Skill ?? "").Trim())
                         .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                         .OrderByDescending(g => g.Count()).Take(3)
                         .Select(g => g.Key).ToList();

            var majorityLevel = targets.GroupBy(u => u.Level ?? "Middle")
                                       .OrderByDescending(g => g.Count())
                                       .First().Key;

            var allQs = await _qRepo.GetAllAsync();
            var pool = FilterBySkills(allQs, skills);
            if (!string.Equals(opt.DifficultyPolicy, "Any", StringComparison.OrdinalIgnoreCase))
                pool = FilterByLevel(pool, majorityLevel);

            var (picked, missing) = PickWithFallback(pool, allQs, skills, opt);
            if (missing > 0 && opt.FailWhenInsufficient)
                throw BuildInsufficientError(missing, opt, allQs, skills, pool);

            var alloc = ScoringAllocator.Allocate(
                picked.Select(p => (p.Id, p.Type.ToString(), p.Difficulty)),
                opt.TotalScore, opt.EssayReserved, opt.EssayCount);

            var map = alloc.ToDictionary(x => x.questionId, x => x.points);
            var items = picked.Select(q => new TestItem { QuestionId = q.Id, Points = map[q.Id] }).ToList();

            var test = new Test
            {
                Title = $"Auto - Dept/Group - {string.Join(",", skills)} - Level:{majorityLevel}",
                DurationMinutes = Math.Max(10, picked.Count * 2),
                PassScore = (int)Math.Round((double)opt.TotalScore / 2.0, MidpointRounding.AwayFromZero),
                ShuffleQuestions = true,
                TotalMaxScore = opt.TotalScore,
                Items = items,
                QuestionIds = items.Select(i => i.QuestionId).ToList(),
                IsPublished = false,
                CreatedBy = actorUserName,
                CreatedAt = DateTime.UtcNow
            };
            await _tRepo.InsertAsync(test);
            return (test, items, targets);
        }

        // === Generate cá nhân hoá — mỗi user 1 Test (DRAFT) ===
        public async System.Threading.Tasks.Task<List<PersonalizedTestResult>> GeneratePersonalizedAsync(
            AutoTestOptions opt, string actorUserName)
        {
            var allUsers = await _uRepo.GetAllAsync();
            var targets = ResolveTargets(allUsers, opt);
            if (targets.Count == 0) throw new InvalidOperationException("Không có người dùng mục tiêu.");

            var allQuestions = await _qRepo.GetAllAsync();
            var results = new List<PersonalizedTestResult>();

            foreach (var u in targets)
            {
                var skillSet = (opt.Skills?.Any() == true)
                    ? new HashSet<string>(opt.Skills!, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(new[] { u.Skill ?? "" }, StringComparer.OrdinalIgnoreCase);

                var basePool = FilterBySkills(allQuestions, skillSet.Where(s => !string.IsNullOrWhiteSpace(s)).ToList());
                if (!string.Equals(opt.DifficultyPolicy, "Any", StringComparison.OrdinalIgnoreCase))
                    basePool = FilterByLevel(basePool, u.Level ?? "Middle");

                var (picked, missing) = PickWithFallback(basePool, allQuestions, skillSet.ToList(), opt);
                if (missing > 0 && opt.FailWhenInsufficient)
                    throw BuildInsufficientError(missing, opt, allQuestions, skillSet.ToList(), basePool, u);

                var alloc = ScoringAllocator.Allocate(
                    picked.Select(p => (p.Id, p.Type.ToString(), p.Difficulty)),
                    opt.TotalScore, opt.EssayReserved, opt.EssayCount);

                var map = alloc.ToDictionary(x => x.questionId, x => x.points);
                var items = picked.Select(q => new TestItem { QuestionId = q.Id, Points = map[q.Id] }).ToList();

                var test = new Test
                {
                    Title = $"Auto - {u.Name} - {u.Skill} - {u.Level}",
                    DurationMinutes = Math.Max(10, picked.Count * 2),
                    PassScore = (int)Math.Round((double)opt.TotalScore / 2.0, MidpointRounding.AwayFromZero),
                    ShuffleQuestions = true,
                    TotalMaxScore = opt.TotalScore,
                    Items = items,
                    QuestionIds = items.Select(i => i.QuestionId).ToList(),
                    IsPublished = false,
                    CreatedBy = actorUserName,
                    CreatedAt = DateTime.UtcNow
                };
                await _tRepo.InsertAsync(test);

                results.Add(new PersonalizedTestResult { User = u, Test = test, Items = items });
            }

            return results;
        }

        // === Generate + assign ngay (API cũ) ===
        public async System.Threading.Tasks.Task<(Test test, List<TestItem> items, List<User> assignedUsers)> GenerateAndAssignAsync(
            AutoTestOptions opt, string actorUserName)
        {
            var (test, items, users) = await GenerateAsync(opt, actorUserName);
            test.IsPublished = true; test.PublishedAt = DateTime.UtcNow;
            await _tRepo.UpsertAsync(x => x.Id == test.Id, test);

            var s = opt.StartAtUtc ?? DateTime.UtcNow.AddDays(-1);
            var e = opt.EndAtUtc ?? DateTime.UtcNow.AddDays(30);

            foreach (var u in users)
            {
                await _aRepo.InsertAsync(new Assignment
                {
                    TestId = test.Id,
                    TargetType = "User",
                    TargetValue = u.Id,
                    StartAt = s,
                    EndAt = e
                });
            }
            return (test, items, users);
        }

        // ----------------- Helpers -----------------
        private static List<User> ResolveTargets(List<User> all, AutoTestOptions opt)
        {
            if (string.Equals(opt.Mode, "Users", StringComparison.OrdinalIgnoreCase))
            {
                var set = new HashSet<string>(opt.UserIds ?? new(), StringComparer.Ordinal);
                return all.Where(u => set.Contains(u.Id)).ToList();
            }
            else
            {
                var dept = opt.Department ?? "";
                return all.Where(u => string.Equals(u.Department ?? "", dept, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private static List<Question> FilterBySkills(List<Question> pool, List<string> skills)
        {
            if (skills == null || skills.Count == 0) return pool;
            var set = new HashSet<string>(skills, StringComparer.OrdinalIgnoreCase);
            return pool.Where(q => set.Contains(q.Skill)).ToList();
        }

        private static List<Question> FilterByLevel(List<Question> pool, string level)
        {
            bool Allowed(string qDiff, string userLevel) => qDiff switch
            {
                "Junior" => userLevel is "Junior" or "Middle",
                "Middle" => userLevel is "Junior" or "Middle" or "Senior",
                "Senior" => userLevel is "Middle" or "Senior",
                _ => true
            };
            var lv = string.IsNullOrWhiteSpace(level) ? "Middle" : level;
            return pool.Where(q => Allowed(q.Difficulty ?? "Middle", lv)).ToList();
        }

        /// <summary>
        /// Pick đủ số câu theo từng loại; nếu thiếu → nới lỏng:
        /// (1) đúng Skill, bỏ phân loại; (2) bỏ luôn Skill. Trả về (picked, missing).
        /// </summary>
        private static (List<Question> picked, int missing) PickWithFallback(
            List<Question> initialPool,
            List<Question> allQuestions,
            List<string> skills,
            AutoTestOptions opt)
        {
            var rnd = new Random();

            List<Question> P(IEnumerable<Question> pool, Func<Question, bool> pred, int n)
                => pool.Where(pred).OrderBy(_ => rnd.Next()).Take(Math.Max(0, n)).ToList();

            int needMCQ = Math.Max(0, opt.McqCount);
            int needTF = Math.Max(0, opt.TfCount);
            int needMT = Math.Max(0, opt.MatchingCount);
            int needDD = Math.Max(0, opt.DragDropCount);
            int needES = Math.Max(0, opt.EssayCount);
            int needTotal = needMCQ + needTF + needMT + needDD + needES;

            var result = new List<Question>();
            result.AddRange(P(initialPool, q => q.Type == QType.MCQ, needMCQ));
            result.AddRange(P(initialPool, q => q.Type == QType.TrueFalse, needTF));
            result.AddRange(P(initialPool, q => q.Type == QType.Matching, needMT));
            result.AddRange(P(initialPool, q => q.Type == QType.DragDrop, needDD));
            result.AddRange(P(initialPool, q => q.Type == QType.Essay, needES));
            result = result.GroupBy(q => q.Id).Select(g => g.First()).ToList();

            int missing = needTotal - result.Count;
            if (missing <= 0) return (result, 0);

            // Nới lỏng 1 — giữ Skill, bỏ phân loại
            var skillSet = new HashSet<string>(skills ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var poolBySkillOnly = allQuestions
                .Where(q => skillSet.Contains(q.Skill))
                .Where(q => !result.Any(r => r.Id == q.Id))
                .OrderBy(_ => rnd.Next())
                .Take(missing)
                .ToList();

            result.AddRange(poolBySkillOnly);
            result = result.GroupBy(q => q.Id).Select(g => g.First()).ToList();

            missing = needTotal - result.Count;
            if (missing <= 0) return (result, 0);

            // Nới lỏng 2 — bỏ luôn Skill (toàn bộ kho)
            var poolAny = allQuestions
                .Where(q => !result.Any(r => r.Id == q.Id))
                .OrderBy(_ => rnd.Next())
                .Take(missing)
                .ToList();

            result.AddRange(poolAny);
            result = result.GroupBy(q => q.Id).Select(g => g.First()).ToList();

            missing = needTotal - result.Count;
            return (result, Math.Max(0, missing));
        }

        /// <summary>Tạo lỗi “không đủ câu” kèm chẩn đoán ngắn gọn cho UI.</summary>
        private static InvalidOperationException BuildInsufficientError(
            int missing,
            AutoTestOptions opt,
            List<Question> allQuestions,
            List<string> skills,
            List<Question> initialPool,
            User? u = null)
        {
            int needTotal = Math.Max(0, opt.McqCount) + Math.Max(0, opt.TfCount)
                          + Math.Max(0, opt.MatchingCount) + Math.Max(0, opt.DragDropCount)
                          + Math.Max(0, opt.EssayCount);

            var skillSet = new HashSet<string>(skills ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            int totalAll = allQuestions.Select(q => q.Id).Distinct().Count();
            int totalBySkill = allQuestions.Where(q => skillSet.Contains(q.Skill))
                                           .Select(q => q.Id).Distinct().Count();
            int totalInitial = initialPool.Select(q => q.Id).Distinct().Count();

            string who = u == null ? "" : $"User: {u.Name} ({u.Skill}/{u.Level}). ";
            string tip = "Gợi ý: thêm câu hỏi vào kho; bỏ bớt/điền thêm Skills; chọn DifficultyPolicy = Any; hoặc giảm số lượng yêu cầu.";
            string msg = $"{who}Không đủ câu hỏi để tạo đề: yêu cầu {needTotal} nhưng hiện chỉ có {needTotal - missing}. " +
                         $"(Kho: All={totalAll}, Theo Skill={totalBySkill}, Pool ban đầu={totalInitial}). {tip}";
            return new InvalidOperationException(msg);
        }
    }
}
