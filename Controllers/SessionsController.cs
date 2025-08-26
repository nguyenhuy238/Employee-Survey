using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Employee_Survey.Controllers
{
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly IRepository<Session> _sRepo;
        private readonly IRepository<Test> _tRepo;
        private readonly IRepository<Question> _qRepo;
        private readonly AssignmentService _assignSvc;

        public SessionsController(IRepository<Session> s,
                                  IRepository<Test> t,
                                  IRepository<Question> q,
                                  AssignmentService assignSvc)
        {
            _sRepo = s; _tRepo = t; _qRepo = q; _assignSvc = assignSvc;
        }

        [HttpGet("/mytests/start/{testId}")]
        public async Task<IActionResult> Start(string testId)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Forbid();

            var test = await _tRepo.FirstOrDefaultAsync(x => x.Id == testId);
            if (test == null || !test.IsPublished) return NotFound();

            var now = DateTime.UtcNow;
            var availIds = await _assignSvc.GetAvailableTestIdsAsync(uid, now);
            if (!availIds.Contains(testId)) return Forbid();

            var sessionsOfUser = (await _sRepo.GetAllAsync()).Where(s => s.UserId == uid && s.TestId == testId).ToList();
            var latestSubmitted = sessionsOfUser
                .Where(s => s.Status != SessionStatus.Draft)
                .OrderByDescending(s => s.EndAt ?? s.StartAt)
                .FirstOrDefault();
            if (latestSubmitted != null)
                return Redirect($"/mytests/result/{latestSubmitted.Id}");

            static DateTime Key(Session s) => (s.LastActivityAt == default ? s.StartAt : s.LastActivityAt);
            var draft = sessionsOfUser
                .Where(s => s.Status == SessionStatus.Draft)
                .OrderByDescending(Key)
                .FirstOrDefault();
            if (draft != null)
                return Redirect($"/mytests/session/{draft.Id}");

            var snapshot = await BuildSnapshotAsync(test);
            var sNew = new Session
            {
                TestId = test.Id,
                UserId = uid,
                StartAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                Status = SessionStatus.Draft,
                TimerStartedAt = DateTime.UtcNow,
                Snapshot = snapshot,
                Answers = new List<Answer>(),
                MaxScore = snapshot.Count
            };
            await _sRepo.InsertAsync(sNew);

            return Redirect($"/mytests/session/{sNew.Id}");
        }

        private async Task<List<Question>> BuildSnapshotAsync(Test t)
        {
            var all = await _qRepo.GetAllAsync();
            var pool = all.Where(q => string.Equals(q.Skill ?? "", t.SkillFilter ?? "", StringComparison.OrdinalIgnoreCase)).ToList();

            if (t.QuestionIds != null && t.QuestionIds.Count > 0)
            {
                var map = pool.ToDictionary(x => x.Id, x => x);
                var list = new List<Question>();
                foreach (var qid in t.QuestionIds)
                    if (map.TryGetValue(qid, out var q)) list.Add(q);
                if (t.ShuffleQuestions) list = list.OrderBy(_ => Guid.NewGuid()).ToList();
                return list;
            }

            var rnd = new Random();
            List<Question> pick(List<Question> src, int n)
                => src.OrderBy(_ => rnd.Next()).Take(Math.Max(0, n)).ToList();

            var mcq = pick(pool.Where(q => q.Type == QType.MCQ).ToList(), t.RandomMCQ);
            var tf = pick(pool.Where(q => q.Type == QType.TrueFalse).ToList(), t.RandomTF);
            var es = pick(pool.Where(q => q.Type == QType.Essay).ToList(), t.RandomEssay);

            var snapshot = new List<Question>();
            snapshot.AddRange(mcq); snapshot.AddRange(tf); snapshot.AddRange(es);

            if (t.ShuffleQuestions) snapshot = snapshot.OrderBy(_ => Guid.NewGuid()).ToList();
            return snapshot;
        }

        [HttpGet("/mytests/session/{id}")]
        public async Task<IActionResult> Runner(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            var t = s is null ? null : await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            if (s == null || t == null) return NotFound();

            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            s.LastActivityAt = DateTime.UtcNow;
            if (!s.TimerStartedAt.HasValue)
                s.TimerStartedAt = DateTime.UtcNow;

            await _sRepo.UpsertAsync(x => x.Id == s.Id, s);

            ViewBag.TestTitle = t.Title;
            ViewBag.Duration = t.DurationMinutes;
            ViewBag.RemainingSeconds = ComputeRemainingSeconds(s, t);
            return View(s);
        }

        [HttpGet("/mytests/result/{id}")]
        public async Task<IActionResult> Result(string id)
        {
            var s = await _sRepo.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null || !string.Equals(uid, s.UserId, StringComparison.Ordinal)) return Forbid();

            var currentTestId = s.TestId;
            await _sRepo.DeleteAsync(x => x.UserId == uid
                                          && x.TestId == currentTestId
                                          && x.Id != s.Id
                                          && x.Status == SessionStatus.Draft);

            var t = await _tRepo.FirstOrDefaultAsync(x => x.Id == s.TestId);
            ViewBag.TestTitle = t?.Title ?? "Result";
            return View(s);
        }

        private int ComputeRemainingSeconds(Session s, Test t)
        {
            var total = Math.Max(1, t.DurationMinutes) * 60;
            var runningDelta = s.TimerStartedAt.HasValue
                ? (int)Math.Floor((DateTime.UtcNow - s.TimerStartedAt.Value).TotalSeconds)
                : 0;
            var consumed = Math.Max(0, s.ConsumedSeconds + runningDelta);
            return Math.Max(0, total - consumed);
        }
    }
}
