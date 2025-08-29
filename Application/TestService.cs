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

            // === Build snapshot + map điểm ===
            var snapshot = await BuildSnapshotAsync(test, all);
            var items = BuildSessionItems(test, snapshot);

            var totalMax = (double)items.Sum(it => it.Points);

            var ses = new Session
            {
                TestId = testId,
                UserId = userId,
                StartAt = DateTime.UtcNow,
                Status = SessionStatus.Draft,
                TimerStartedAt = DateTime.UtcNow,
                Snapshot = snapshot,
                Answers = new List<Answer>(),
                Items = items,
                AutoScore = 0,
                ManualScore = 0,
                TotalScore = 0,
                MaxScore = Math.Round(totalMax, 2),
                Percent = 0
            };
            await _sRepo.InsertAsync(ses);
            return ses;
        }

        public async Task<Session> SubmitAsync(string sessionId, Dictionary<string, string?> answers)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == sessionId) ?? throw new Exception("Session not found");
            if (s.Status == SessionStatus.Submitted) return s;

            var test = await _tRepo.FirstOrDefaultAsync(t => t.Id == s.TestId) ?? throw new Exception("Test not found");

            double autoScore = 0.0;
            double autoMax = 0.0; // tổng điểm phần auto
            double manualMax = 0.0; // tổng điểm essay

            var ans = new List<Answer>();

            // Tạo map điểm theo câu hỏi
            var pointsByQ = s.Items.ToDictionary(i => i.QuestionId, i => (double)i.Points);

            foreach (var q in s.Snapshot)
            {
                answers.TryGetValue(q.Id, out var selRaw);
                selRaw ??= "";

                double grade = 0.0; // 0..1
                bool isAuto = true;

                switch (q.Type)
                {
                    case QType.MCQ:
                    case QType.TrueFalse:
                        {
                            var correct = q.CorrectKeys ?? new List<string>();
                            var sel = selRaw.Trim();
                            grade = (!string.IsNullOrEmpty(sel) && correct.Any() && correct.Contains(sel)) ? 1.0 : 0.0;
                            ans.Add(new Answer { QuestionId = q.Id, Selected = sel, Score = grade });
                            break;
                        }
                    case QType.Matching:
                        {
                            var pairs = q.MatchingPairs ?? new List<MatchPair>();
                            var given = ParsePairs(selRaw);
                            var correctCount = pairs.Count(p => given.TryGetValue(p.L, out var r) && string.Equals(r, p.R, StringComparison.Ordinal));
                            grade = pairs.Count == 0 ? 0 : (double)correctCount / pairs.Count;
                            var normalized = string.Join("|", given.Select(kv => $"{kv.Key}={kv.Value}"));
                            ans.Add(new Answer { QuestionId = q.Id, Selected = normalized, Score = Math.Round(grade, 4) });
                            break;
                        }
                    case QType.DragDrop:
                        {
                            var slots = q.DragDrop?.Slots ?? new List<DragSlot>();
                            var given = ParsePairs(selRaw);
                            var correctCount = slots.Count(slt => given.TryGetValue(slt.Name, out var tok) && string.Equals(tok, slt.Answer, StringComparison.Ordinal));
                            grade = slots.Count == 0 ? 0 : (double)correctCount / slots.Count;
                            var normalized = string.Join("|", given.Select(kv => $"{kv.Key}={kv.Value}"));
                            ans.Add(new Answer { QuestionId = q.Id, Selected = normalized, Score = Math.Round(grade, 4) });
                            break;
                        }
                    case QType.Essay:
                    default:
                        {
                            // Essay không auto-grade
                            isAuto = false;
                            ans.Add(new Answer { QuestionId = q.Id, TextAnswer = selRaw, Score = 0 });
                            break;
                        }
                }

                var qPoints = pointsByQ.TryGetValue(q.Id, out var p) ? p : 1.0;

                if (isAuto)
                {
                    autoScore += grade * qPoints;
                    autoMax += qPoints;
                }
                else
                {
                    manualMax += qPoints; // chờ chấm tay
                }
            }

            s.EndAt = DateTime.UtcNow;
            s.Status = SessionStatus.Submitted;
            s.Answers = ans;

            s.AutoScore = Math.Round(autoScore, 2);
            // ManualScore giữ nguyên (0) cho đến khi HR/Manager chấm
            s.TotalScore = Math.Round(s.AutoScore + s.ManualScore, 2);

            s.MaxScore = Math.Round(autoMax + manualMax, 2);
            s.Percent = s.MaxScore > 0 ? Math.Round((s.TotalScore / s.MaxScore) * 100.0, 2) : 0;

            // Đậu nếu TotalScore >= PassScore (PassScore tính theo "điểm")
            s.IsPassed = s.TotalScore >= test.PassScore;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);
            return s;
        }

        // ===== Helpers =====
        private static Dictionary<string, string> ParsePairs(string raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in (raw ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split('=', StringSplitOptions.TrimEntries);
                if (kv.Length == 2 && !string.IsNullOrWhiteSpace(kv[0]))
                    dict[kv[0]] = kv[1];
            }
            return dict;
        }

        private async Task<List<Question>> BuildSnapshotAsync(Test t, List<Question> all)
        {
            // Ưu tiên theo Items (điểm)
            if (t.Items is { Count: > 0 })
            {
                var map = all.ToDictionary(x => x.Id, x => x);
                var list = new List<Question>();
                foreach (var it in t.Items)
                    if (map.TryGetValue(it.QuestionId, out var q)) list.Add(q);
                if (t.ShuffleQuestions) list = list.OrderBy(_ => Guid.NewGuid()).ToList();
                return list;
            }

            // Nếu có QuestionIds -> lấy theo ALL (không lọc skill)
            if (t.QuestionIds is { Count: > 0 })
            {
                var map = all.ToDictionary(x => x.Id, x => x);
                var list = new List<Question>();
                foreach (var qid in t.QuestionIds)
                    if (map.TryGetValue(qid, out var q)) list.Add(q);
                if (t.ShuffleQuestions) list = list.OrderBy(_ => Guid.NewGuid()).ToList();
                return list;
            }

            // Ngược lại: random theo SkillFilter
            var pool = all.Where(q => string.Equals(q.Skill ?? "", t.SkillFilter ?? "", StringComparison.OrdinalIgnoreCase)).ToList();

            var rnd = new Random();
            List<Question> pick(List<Question> src, int n)
                => src.OrderBy(_ => rnd.Next()).Take(Math.Max(0, n)).ToList();

            var mcq = pick(pool.Where(q => q.Type == QType.MCQ).ToList(), t.RandomMCQ);
            var tf = pick(pool.Where(q => q.Type == QType.TrueFalse).ToList(), t.RandomTF);
            var es = pick(pool.Where(q => q.Type == QType.Essay).ToList(), t.RandomEssay);

            var snapshot = new List<Question>(); snapshot.AddRange(mcq); snapshot.AddRange(tf); snapshot.AddRange(es);
            if (t.ShuffleQuestions) snapshot = snapshot.OrderBy(_ => Guid.NewGuid()).ToList();
            return snapshot;
        }


        private static List<SessionItem> BuildSessionItems(Test t, List<Question> snapshot)
        {
            var items = new List<SessionItem>();

            if (t.Items != null && t.Items.Count > 0)
            {
                var map = t.Items.ToDictionary(i => i.QuestionId, i => i.Points);
                foreach (var q in snapshot)
                {
                    var pts = map.TryGetValue(q.Id, out var p) ? p : 1m;
                    items.Add(new SessionItem { QuestionId = q.Id, Points = pts });
                }
            }
            else if (t.QuestionIds != null && t.QuestionIds.Count > 0)
            {
                // Fallback: nếu chỉ có QuestionIds => mỗi câu 1 điểm
                foreach (var q in snapshot)
                    items.Add(new SessionItem { QuestionId = q.Id, Points = 1m });
            }
            else
            {
                // Random cổ điển: mỗi câu 1 điểm
                foreach (var q in snapshot)
                    items.Add(new SessionItem { QuestionId = q.Id, Points = 1m });
            }
            return items;
        }
    }
}
