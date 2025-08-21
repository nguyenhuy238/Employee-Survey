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

            IEnumerable<Question> pick(QType type, int count) =>
                all.Where(q => q.Type == type && (test.SkillFilter == "" || q.Skill == test.SkillFilter))
                   .OrderBy(_ => Guid.NewGuid()).Take(count);

            var snapshot = pick(QType.MCQ, test.RandomMCQ)
                         .Concat(pick(QType.Multiple, test.RandomTF))
                         .Concat(pick(QType.Essay, test.RandomEssay))
                         .ToList();

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
                if (q.Type == QType.MCQ || q.Type == QType.Multiple)
                    score = (sel == q.Correct) ? 1 : 0; // mỗi câu 1 điểm
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
