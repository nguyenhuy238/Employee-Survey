using System.ComponentModel.DataAnnotations;
using Employee_Survey.Application;
using Employee_Survey.Domain;

namespace Employee_Survey.Infrastructure;

public class QuestionService : IQuestionService
{
    private readonly IRepository<Question> _qRepo;
    private readonly IRepository<Test> _tRepo;
    private readonly IAuditService _audit;

    public QuestionService(IRepository<Question> qRepo, IRepository<Test> tRepo, IAuditService audit)
    { _qRepo = qRepo; _tRepo = tRepo; _audit = audit; }

    public async Task<PagedResult<Question>> SearchAsync(QuestionFilter f)
    {
        var data = await _qRepo.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(f.Keyword))
            data = data.Where(x => x.Content.Contains(f.Keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        if (f.Type.HasValue) data = data.Where(x => x.Type == f.Type.Value).ToList();
        if (!string.IsNullOrWhiteSpace(f.Skill)) data = data.Where(x => x.Skill == f.Skill).ToList();
        if (!string.IsNullOrWhiteSpace(f.Difficulty)) data = data.Where(x => x.Difficulty == f.Difficulty).ToList();
        if (!string.IsNullOrWhiteSpace(f.TagsCsv))
        {
            var tags = f.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            data = data.Where(x => x.Tags.Any(t => tags.Contains(t))).ToList();
        }
        if (f.From.HasValue) data = data.Where(x => x.CreatedAt >= f.From.Value).ToList();
        if (f.To.HasValue) data = data.Where(x => x.CreatedAt <= f.To.Value).ToList();

        data = f.Sort switch
        {
            "Content_asc" => data.OrderBy(x => x.Content).ToList(),
            "Content_desc" => data.OrderByDescending(x => x.Content).ToList(),
            "CreatedAt_asc" => data.OrderBy(x => x.CreatedAt).ToList(),
            _ => data.OrderByDescending(x => x.CreatedAt).ToList()
        };

        var total = data.Count;
        var items = data.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();
        return new PagedResult<Question> { Page = f.Page, PageSize = f.PageSize, Total = total, Items = items };
    }

    public Task<Question?> GetAsync(string id) => _qRepo.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<string> CreateAsync(Question q, string actor)
    {
        ValidateQuestion(q, isNew: true);
        q.CreatedBy = actor; q.CreatedAt = DateTime.UtcNow;
        await _qRepo.InsertAsync(q);
        await _audit.LogAsync(actor, "Question.Create", q.Id, before: null, after: q);
        return q.Id;
    }

    public async Task<(bool Success, string? Reason)> UpdateAsync(Question q, string actor)
    {
        var tests = await _tRepo.GetAllAsync();
        var usedByPublished = tests.Any(t => t.IsPublished && t.QuestionIds.Contains(q.Id));
        if (usedByPublished)
            return (false, "Question is used in a Published Test. Only cloning or minor edits allowed.");

        var before = await GetAsync(q.Id);
        if (before == null) return (false, "Not found");

        ValidateQuestion(q, isNew: false);
        q.UpdatedBy = actor; q.UpdatedAt = DateTime.UtcNow;
        await _qRepo.UpsertAsync(x => x.Id == q.Id, q);
        await _audit.LogAsync(actor, "Question.Update", q.Id, before, q);
        return (true, null);
    }

    public async Task<(bool Success, string? Reason)> DeleteAsync(string id, string actor)
    {
        var tests = await _tRepo.GetAllAsync();
        if (tests.Any(t => t.IsPublished && t.QuestionIds.Contains(id)))
            return (false, "Question is used in a Published Test. Delete is blocked.");

        var before = await GetAsync(id);
        if (before == null) return (false, "Not found");

        await _qRepo.DeleteAsync(x => x.Id == id);
        await _audit.LogAsync(actor, "Question.Delete", id, before, after: null);
        return (true, null);
    }

    public async Task<string> CloneAsync(string id, string actor)
    {
        var q = await GetAsync(id) ?? throw new Exception("Not found");
        var copy = System.Text.Json.JsonSerializer.Deserialize<Question>(
            System.Text.Json.JsonSerializer.Serialize(q))!;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Content = "[CLONE] " + copy.Content;
        copy.CreatedAt = DateTime.UtcNow; copy.CreatedBy = actor;
        copy.UpdatedAt = null; copy.UpdatedBy = null;
        await _qRepo.InsertAsync(copy);
        await _audit.LogAsync(actor, "Question.Clone", copy.Id, before: null, after: copy);
        return copy.Id;
    }

    private static void ValidateQuestion(Question q, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(q.Content)) throw new ValidationException("Content required");
        switch (q.Type)
        {
            case QType.MCQ:
                if (q.Options == null || q.Options.Count < 2) throw new ValidationException("MCQ requires >= 2 options");
                if (q.CorrectKeys == null || q.CorrectKeys.Count == 0) throw new ValidationException("MCQ requires at least 1 correct");
                break;
            case QType.TrueFalse:
                q.Options = new() { "True", "False" };
                if (q.CorrectKeys == null || q.CorrectKeys.Count != 1 || !(q.CorrectKeys[0] is "True" or "False"))
                    throw new ValidationException("True/False requires exactly one of True/False as correct");
                break;
            case QType.Matching:
                if (q.MatchingPairs == null || q.MatchingPairs.Count == 0) throw new ValidationException("Matching requires at least 1 pair");
                break;
            case QType.DragDrop:
                if (q.DragDrop == null || q.DragDrop.Slots.Count == 0 || q.DragDrop.Tokens.Count == 0)
                    throw new ValidationException("DragDrop requires tokens and slots");
                break;
        }
    }
}
