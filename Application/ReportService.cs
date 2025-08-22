using Employee_Survey.Models;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class ReportService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Test> _testRepo;
        private readonly IRepository<Assignment> _asRepo;
        private readonly IRepository<Session> _sesRepo;

        public ReportService(IRepository<User> u, IRepository<Test> t, IRepository<Assignment> a, IRepository<Session> s)
        {
            _userRepo = u; _testRepo = t; _asRepo = a; _sesRepo = s;
        }

        public async Task<HrDashboardViewModel> GetHrDashboardAsync(DateTime nowUtc)
        {
            var users = await _userRepo.GetAllAsync();
            var tests = await _testRepo.GetAllAsync();
            var assigns = await _asRepo.GetAllAsync();
            var sessions = await _sesRepo.GetAllAsync();

            // Cards
            var totalEmployees = users.Count(x => x.Role == Role.Employee);
            var totalTests = tests.Count;
            
            // Chỉ đếm active assignments cho các test đã publish
            var publishedTestIds = tests.Where(t => t.IsPublished).Select(t => t.Id).ToHashSet();
            var activeAssigns = assigns.Count(a => a.StartAt <= nowUtc && nowUtc <= a.EndAt && 
                                                  publishedTestIds.Contains(a.TestId));

            // Pass rate (dựa theo Test.PassScore)
            var submitted = sessions.Where(s => s.Status != SessionStatus.Draft && s.EndAt != null).ToList();
            int passed = 0;
            if (submitted.Any())
            {
                // build dict TestId -> PassScore
                var passMap = tests.ToDictionary(t => t.Id, t => t.PassScore);
                passed = submitted.Count(s => passMap.TryGetValue(s.TestId, out var pass) && s.TotalScore >= pass);
            }
            var passRate = submitted.Count == 0 ? 0 : (100.0 * passed / submitted.Count);

            // Active assignments list - chỉ hiển thị cho test đã publish
            var activeList = assigns
                .Where(a => a.StartAt <= nowUtc && nowUtc <= a.EndAt && publishedTestIds.Contains(a.TestId))
                .Select(a => new HrDashboardViewModel.ActiveAssignmentRow
                {
                    TestId = a.TestId,
                    TestTitle = tests.FirstOrDefault(t => t.Id == a.TestId)?.Title ?? a.TestId,
                    Target = $"{a.TargetType}:{a.TargetValue}",
                    StartAt = a.StartAt,
                    EndAt = a.EndAt
                }).OrderBy(x => x.EndAt).Take(20).ToList();

            // Recent submissions (10 gần nhất)
            var recent = submitted
                .OrderByDescending(s => s.EndAt)
                .Take(10)
                .Select(s => new HrDashboardViewModel.RecentSubmissionRow
                {
                    SessionId = s.Id,
                    UserName = users.FirstOrDefault(u => u.Id == s.UserId)?.Name ?? s.UserId,
                    TestTitle = tests.FirstOrDefault(t => t.Id == s.TestId)?.Title ?? s.TestId,
                    Score = s.TotalScore,
                    IsPass = tests.FirstOrDefault(t => t.Id == s.TestId)?.PassScore is int p && s.TotalScore >= p,
                    EndAt = s.EndAt!.Value
                }).ToList();

            // Top skills (đếm theo Snapshot các phiên đã làm)
            var skillCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in submitted)
            {
                foreach (var q in s.Snapshot)
                {
                    var skill = string.IsNullOrWhiteSpace(q.Skill) ? "(Unknown)" : q.Skill;
                    if (!skillCount.ContainsKey(skill)) skillCount[skill] = 0;
                    skillCount[skill]++;
                }
            }
            var skills = skillCount.Select(kv => new HrDashboardViewModel.SkillStat { Skill = kv.Key, Count = kv.Value })
                                   .OrderByDescending(x => x.Count).Take(8).ToList();

            return new HrDashboardViewModel
            {
                TotalEmployees = totalEmployees,
                TotalTests = totalTests,
                ActiveAssignments = activeAssigns,
                PassRatePercent = Math.Round(passRate, 1),
                ActiveAssignmentsList = activeList,
                RecentSubmissions = recent,
                TopSkills = skills
            };
        }

        // (Tùy chọn) xuất CSV nhanh
        public async Task<(string fileName, string csv)> ExportRecentSubmissionsCsvAsync()
        {
            var now = DateTime.UtcNow;
            var vm = await GetHrDashboardAsync(now);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SessionId,User,Test,Score,IsPass,EndAt");
            foreach (var r in vm.RecentSubmissions)
                sb.AppendLine($"{r.SessionId},{Escape(r.UserName)},{Escape(r.TestTitle)},{r.Score},{r.IsPass},{r.EndAt:O}");
            return ($"recent-submissions-{now:yyyyMMddHHmmss}.csv", sb.ToString());

            static string Escape(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
