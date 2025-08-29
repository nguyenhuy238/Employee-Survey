using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Employee_Survey.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Admin,HR,Manager")]
    public class GradingController : Controller
    {
        private readonly IRepository<Session> _sRepo;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Question> _qRepo;

        public GradingController(IRepository<Session> s, IRepository<Test> t, IRepository<Question> q)
        { _sRepo = s; _tRepo = t; _qRepo = q; }

        [HttpGet("/grading/pending")]
        public async Task<IActionResult> Pending()
        {
            var all = await _sRepo.GetAllAsync();
            var list = all
                .Where(s => s.Snapshot.Any(q => q.Type == QType.Essay))
                .Where(s => s.Items.Any())
                .Where(s => s.Status == SessionStatus.Submitted || s.Status == SessionStatus.Graded)
                .OrderByDescending(s => s.EndAt ?? s.StartAt)
                .ToList();
            return View(list);
        }

        [HttpGet("/grading/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId) ?? new Test();
            var itemPoints = s.Items.ToDictionary(i => i.QuestionId, i => i.Points);

            var essayQs = s.Snapshot.Where(q => q.Type == QType.Essay).ToList();
            var mapAns = s.Answers.ToDictionary(a => a.QuestionId, a => a);

            var vm = new GradeSessionViewModel
            {
                Session = s,
                Test = t,
                Essays = essayQs.Select(q =>
                {
                    mapAns.TryGetValue(q.Id, out var a);
                    return new GradeSessionViewModel.EssayItem
                    {
                        QuestionId = q.Id,
                        Content = q.Content,
                        MaxPoints = itemPoints.TryGetValue(q.Id, out var p) ? p : 0m,
                        UserAnswer = a?.TextAnswer,
                        GivenScore = a?.Score
                    };
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost("/grading/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [FromForm] Dictionary<string, double> scores)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var itemPoints = s.Items.ToDictionary(i => i.QuestionId, i => (double)i.Points);
            var answers = s.Answers;

            // Ghi điểm từng Essay vào Answer.Score
            foreach (var kv in scores)
            {
                var qid = kv.Key;
                var valRaw = kv.Value;
                var max = itemPoints.TryGetValue(qid, out var p) ? p : 0.0;

                var val = Math.Max(0, Math.Min(max, valRaw));

                var ans = answers.FirstOrDefault(a => a.QuestionId == qid);
                if (ans == null)
                {
                    ans = new Answer { QuestionId = qid, TextAnswer = "" };
                    answers.Add(ans);
                }
                ans.Score = val;
            }

            // ManualScore = tổng điểm Essay
            var essayIds = s.Snapshot.Where(q => q.Type == QType.Essay).Select(q => q.Id).ToHashSet();
            s.ManualScore = Math.Round(answers.Where(a => essayIds.Contains(a.QuestionId)).Sum(a => a.Score), 2);

            // Tính lại tổng/percent/pass
            s.MaxScore = (double)s.Items.Sum(i => i.Points);
            s.TotalScore = Math.Round(s.AutoScore + s.ManualScore, 2);
            s.Percent = s.MaxScore > 0 ? Math.Round(s.TotalScore * 100.0 / s.MaxScore, 2) : 0;

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            var passScore = t?.PassScore ?? 0;
            s.IsPassed = s.TotalScore >= passScore;

            s.Status = SessionStatus.Graded;
            s.LastActivityAt = DateTime.UtcNow;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);

            TempData["Msg"] = "Đã lưu điểm Essay.";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
