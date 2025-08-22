// File: Application/TestService.cs
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class TestService
    {
        private readonly IRepository<Question> _qRepo;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Session> _sRepo;

        public TestService(IRepository<Question> qRepo, IRepository<Test> tRepo, IRepository<Session> sRepo)
        { _qRepo = qRepo; _tRepo = tRepo; _sRepo = sRepo; }

        public async Task<Session> StartAsync(string testId, string userId)
        {
            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == testId) ?? throw new Exception("Test not found");
            var all = await _qRepo.GetAllAsync();

            List<Question> snapshot;

            if (test.QuestionIds != null && test.QuestionIds.Count > 0)
            {
                // Lấy theo danh sách đã “đóng băng”
                var map = all.ToDictionary(x => x.Id, x => x);
                snapshot = test.QuestionIds.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToList();

                if (test.ShuffleQuestions)
                    snapshot = snapshot.OrderBy(_ => Guid.NewGuid()).ToList();
            }
            else
            {
                // Random theo cấu hình
                IEnumerable<Question> pick(QType type, int count) =>
                    all.Where(q => q.Type == type && (string.IsNullOrWhiteSpace(test.SkillFilter) || q.Skill == test.SkillFilter))
                       .OrderBy(_ => Guid.NewGuid()).Take(count);

                snapshot = pick(QType.MCQ, test.RandomMCQ)
                           .Concat(pick(QType.TrueFalse, test.RandomTF))
                           .Concat(pick(QType.Essay, test.RandomEssay))
                           .ToList();
            }

            var ses = new Session
            {
                TestId = testId,
                UserId = userId,
                StartAt = DateTime.UtcNow,
                Status = SessionStatus.Draft,
                Snapshot = snapshot
            };
            await _sRepo.InsertAsync(ses);
            return ses;
        }

        public async Task<Session> SubmitAsync(string sessionId, Dictionary<string, string?> answers)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == sessionId) ?? throw new Exception("Session not found");
            double total = 0; var ans = new List<Answer>();

            foreach (var q in s.Snapshot)
            {
                answers.TryGetValue(q.Id, out var sel);
                double score = 0;
                if (q.Type == QType.MCQ || q.Type == QType.TrueFalse)
                {
                    var correct = q.CorrectKeys ?? new List<string>();
                    if (!string.IsNullOrEmpty(sel) && correct.Any())
                        score = correct.Contains(sel) ? 1 : 0;
                }
                else if (q.Type == QType.Essay)
                {
                    score = 0; // chưa hỗ trợ chấm tự động
                }
                ans.Add(new Answer { QuestionId = q.Id, Selected = sel, Score = score });
                total += score;
            }

            s.EndAt = DateTime.UtcNow;
            s.Status = SessionStatus.Submitted;
            s.TotalScore = total;
            s.Answers = ans;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);
            return s;
        }
    }
}
