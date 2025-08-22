using ClosedXML.Excel;
using Employee_Survey.Application;
using Employee_Survey.Domain;

namespace Employee_Survey.Infrastructure;

public class QuestionExcelService : IQuestionExcelService
{
    private readonly IQuestionService _svc;
    public QuestionExcelService(IQuestionService svc) => _svc = svc;

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

        var rows = range.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            res.Total++;
            try
            {
                var q = new Question
                {
                    Content = row.Cell(1).GetString().Trim(),
                    Type = Enum.Parse<QType>(row.Cell(2).GetString().Trim(), ignoreCase: true),
                    Skill = row.Cell(3).GetString().Trim(),
                    Difficulty = row.Cell(4).GetString().Trim(),
                    Tags = row.Cell(5).GetString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    Options = row.Cell(6).GetString().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    CorrectKeys = row.Cell(7).GetString().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                };
                await _svc.CreateAsync(q, actor);
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
