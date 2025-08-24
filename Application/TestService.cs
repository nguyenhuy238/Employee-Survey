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
                var map = all.ToDictionary(x => x.Id, x => x);
                snapshot = test.QuestionIds.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToList();

                if (test.ShuffleQuestions)
                    snapshot = snapshot.OrderBy(_ => Guid.NewGuid()).ToList();
            }
            else
            {
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

            // Không cho nộp lại nếu đã nộp
            if (s.Status == SessionStatus.Submitted)
                return s;

            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == s.TestId) ?? throw new Exception("Test not found");

            double total = 0;
            double max = 0;
            var ans = new List<Answer>();

            foreach (var q in s.Snapshot)
            {
                answers.TryGetValue(q.Id, out var selRaw);
                selRaw ??= "";

                double score = 0.0;
                double qMax = 0.0;

                switch (q.Type)
                {
                    case QType.MCQ:
                    case QType.TrueFalse:
                        {
                            qMax = 1.0;
                            var correct = q.CorrectKeys ?? new List<string>();
                            var sel = selRaw.Trim();
                            score = (!string.IsNullOrEmpty(sel) && correct.Any() && correct.Contains(sel)) ? 1.0 : 0.0;
                            ans.Add(new Answer { QuestionId = q.Id, Selected = sel, Score = score });
                            break;
                        }

                    case QType.Matching:
                        {
                            var pairs = q.MatchingPairs ?? new List<MatchPair>();
                            if (pairs.Count > 0)
                            {
                                // Format client gửi lên: "Left=Right|Left2=Right2|..."
                                // Ví dụ: "HTTP=Protocol|IIS=WebServer"
                                var given = ParsePairs(selRaw);
                                qMax = 1.0;
                                var correctCount = pairs.Count(p => given.TryGetValue(p.L, out var r) && string.Equals(r, p.R, StringComparison.Ordinal));
                                score = pairs.Count == 0 ? 0 : (double)correctCount / pairs.Count; // điểm tỉ lệ
                                                                                                   // Lưu lại selection dạng "L=R|..."
                                var normalized = string.Join("|", given.Select(kv => $"{kv.Key}={kv.Value}"));
                                ans.Add(new Answer { QuestionId = q.Id, Selected = normalized, Score = Math.Round(score, 4) });
                            }
                            else
                            {
                                ans.Add(new Answer { QuestionId = q.Id, Selected = "", Score = 0 });
                            }
                            break;
                        }

                    case QType.DragDrop:
                        {
                            var slots = q.DragDrop?.Slots ?? new List<DragSlot>();
                            if (slots.Count > 0)
                            {
                                // Format client gửi lên: "SlotName=Token|Slot2=Token2|..."
                                var given = ParsePairs(selRaw);
                                qMax = 1.0;
                                var correctCount = slots.Count(slt => given.TryGetValue(slt.Name, out var tok) && string.Equals(tok, slt.Answer, StringComparison.Ordinal));
                                score = (double)correctCount / slots.Count;
                                var normalized = string.Join("|", given.Select(kv => $"{kv.Key}={kv.Value}"));
                                ans.Add(new Answer { QuestionId = q.Id, Selected = normalized, Score = Math.Round(score, 4) });
                            }
                            else
                            {
                                ans.Add(new Answer { QuestionId = q.Id, Selected = "", Score = 0 });
                            }
                            break;
                        }

                    case QType.Essay:
                    default:
                        {
                            // Essay không auto-grade: giữ Score=0, để chấm tay
                            ans.Add(new Answer { QuestionId = q.Id, TextAnswer = selRaw, Score = 0 });
                            break;
                        }
                }

                total += score;
                max += qMax == 0 ? (q.Type == QType.Essay ? 0 : 1) : qMax; // qMax=1 cho MCQ/TF; Matching/DragDrop đã set 1; Essay=0
            }

            s.EndAt = DateTime.UtcNow;
            s.Status = SessionStatus.Submitted;
            s.TotalScore = Math.Round(total, 4);
            s.MaxScore = Math.Round(max, 4);
            s.Percent = max > 0 ? Math.Round((total / max) * 100.0, 2) : 0;
            s.Answers = ans;

            // Đậu nếu TotalScore >= PassScore (PassScore là theo "điểm câu hỏi", không phải %)
            s.IsPassed = s.TotalScore >= test.PassScore;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);
            return s;
        }

        private static Dictionary<string, string> ParsePairs(string raw)
        {
            // parse "A=B|C=D" -> {A:B, C:D}
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in (raw ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split('=', StringSplitOptions.TrimEntries);
                if (kv.Length == 2 && !string.IsNullOrWhiteSpace(kv[0]))
                    dict[kv[0]] = kv[1];
            }
            return dict;
        }
    }
}
