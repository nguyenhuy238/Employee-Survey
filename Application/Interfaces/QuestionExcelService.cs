using ClosedXML.Excel;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using System.Globalization;

namespace Employee_Survey.Infrastructure;

public class QuestionExcelService : IQuestionExcelService
{
    private readonly IQuestionService _svc;
    private readonly IRepository<Question> _qRepo; // NEW: đọc dữ liệu hiện có để chống trùng

    public QuestionExcelService(IQuestionService svc, IRepository<Question> qRepo)
    {
        _svc = svc;
        _qRepo = qRepo;
    }

    public Task<byte[]> ExportAsync(IEnumerable<Question> data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        ws.Cell(1, 1).Value = "Content";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "Skill";
        ws.Cell(1, 4).Value = "Difficulty";
        ws.Cell(1, 5).Value = "Tags (comma)";
        ws.Cell(1, 6).Value = "Options (|)";
        ws.Cell(1, 7).Value = "CorrectKeys (|)";

        int r = 2;
        foreach (var q in data)
        {
            ws.Cell(r, 1).Value = q.Content;
            ws.Cell(r, 2).Value = q.Type.ToString();
            ws.Cell(r, 3).Value = q.Skill;
            ws.Cell(r, 4).Value = q.Difficulty;
            ws.Cell(r, 5).Value = string.Join(',', q.Tags ?? new());
            ws.Cell(r, 6).Value = string.Join('|', q.Options ?? new());
            ws.Cell(r, 7).Value = string.Join('|', q.CorrectKeys ?? new());
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public async Task<ImportResult> ImportAsync(Stream fileStream, string actor)
    {
        var res = new ImportResult();
        using var wb = new XLWorkbook(fileStream);
        var ws = wb.Worksheets.Worksheet(1);
        var range = ws.RangeUsed();
        if (range == null) return res;

        // --- Load tất cả câu hỏi hiện có để so trùng ---
        var existing = await _qRepo.GetAllAsync();
        static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
        static string MakeKey(string content, QType type, string skill)
            => $"{Norm(content)}|{type}|{Norm(skill)}";

        var existingKeys = existing
            .Select(q => MakeKey(q.Content, q.Type, q.Skill))
            .ToHashSet(StringComparer.Ordinal);

        // Chống trùng lặp ngay trong file import hiện tại
        var seenInThisFile = new HashSet<string>(StringComparer.Ordinal);

        var rows = range.RowsUsed().Skip(1); // bỏ header
        foreach (var row in rows)
        {
            res.Total++;
            try
            {
                var content = row.Cell(1).GetString().Trim();
                var typeRaw = row.Cell(2).GetString().Trim();
                var skill = row.Cell(3).GetString().Trim();

                if (string.IsNullOrWhiteSpace(content))
                    throw new Exception("Content is empty");
                if (string.IsNullOrWhiteSpace(typeRaw))
                    throw new Exception("Type is empty");
                if (!Enum.TryParse<QType>(typeRaw, true, out var type))
                    throw new Exception($"Invalid Type: {typeRaw}");

                var key = MakeKey(content, type, skill);

                // --- Check trùng: đã tồn tại trong DB hoặc trùng trong cùng file ---
                if (existingKeys.Contains(key) || seenInThisFile.Contains(key))
                {
                    res.Skipped++;
                    res.SkippedReasons.Add($"Row {row.RowNumber()}: skipped duplicate (Content,Type,Skill).");
                    continue;
                }

                // Parse các cột còn lại (để Create)
                var difficulty = row.Cell(4).GetString().Trim();
                var tagsCsv = row.Cell(5).GetString();
                var optionsStr = row.Cell(6).GetString();
                var correctStr = row.Cell(7).GetString();

                var q = new Question
                {
                    Content = content,
                    Type = type,
                    Skill = skill,
                    Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "Junior" : difficulty,
                    Tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    Options = optionsStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    CorrectKeys = correctStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                };

                // Lưu — QuestionService sẽ validate theo Type (TF auto-setup Options True/False, v.v.)
                await _svc.CreateAsync(q, actor);

                // ghi nhận key để chống trùng tiếp
                existingKeys.Add(key);
                seenInThisFile.Add(key);
                res.Success++;
            }
            catch (Exception ex)
            {
                res.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
            }
        }

        return res;
    }
}
