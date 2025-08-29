using ClosedXML.Excel;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using System.Text;

namespace Employee_Survey.Infrastructure;

public class QuestionExcelService : IQuestionExcelService
{
    private readonly IQuestionService _svc;
    private readonly IRepository<Question> _qRepo;

    public QuestionExcelService(IQuestionService svc, IRepository<Question> qRepo)
    {
        _svc = svc;
        _qRepo = qRepo;
    }

    // ------------------ EXPORT ------------------
    public Task<byte[]> ExportAsync(IEnumerable<Question> data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");

        // Header
        var headers = new[]
        {
            "Content",
            "Type",
            "Skill",
            "Difficulty",
            "Tags (comma)",
            "Options (|)",
            "CorrectKeys (|)",
            "EssayMinWords",
            "MatchingPairs (L=R || L=R)",
            "DragTokens (|)",
            "DragSlots (Name=Answer || Name=Answer)",
            "Media (FileName#Url#Caption || ...)"
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int r = 2;
        foreach (var q in data)
        {
            ws.Cell(r, 1).Value = q.Content;
            ws.Cell(r, 2).Value = q.Type.ToString();
            ws.Cell(r, 3).Value = q.Skill;
            ws.Cell(r, 4).Value = q.Difficulty;

            ws.Cell(r, 5).Value = string.Join(',', q.Tags ?? new());

            // Options & CorrectKeys
            ws.Cell(r, 6).Value = string.Join('|', q.Options ?? new());
            ws.Cell(r, 7).Value = string.Join('|', q.CorrectKeys ?? new());

            // Essay
            ws.Cell(r, 8).SetValue(q.EssayMinWords.HasValue ? q.EssayMinWords.Value : 0);

            // Matching
            var matching = q.MatchingPairs is { Count: > 0 }
                ? string.Join(" || ", q.MatchingPairs!.Select(p => $"{p.L}={p.R}"))
                : "";
            ws.Cell(r, 9).Value = matching;

            // DragDrop
            var tokens = q.DragDrop?.Tokens ?? new List<string>();
            var slots = q.DragDrop?.Slots ?? new List<DragSlot>();
            ws.Cell(r, 10).Value = string.Join('|', tokens);
            ws.Cell(r, 11).Value = slots.Count > 0 ? string.Join(" || ", slots.Select(s => $"{s.Name}={s.Answer}")) : "";

            // Media
            // Xuất theo format: FileName#Url#Caption || ...
            var media = q.Media is { Count: > 0 }
                ? string.Join(" || ", q.Media.Select(m => $"{m.FileName}#{m.Url}#{m.Caption ?? ""}"))
                : "";
            ws.Cell(r, 12).Value = media;

            r++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    // ------------------ IMPORT ------------------
    public async Task<ImportResult> ImportAsync(Stream fileStream, string actor)
    {
        var res = new ImportResult();
        using var wb = new XLWorkbook(fileStream);

        // Sheet 1 = Questions
        if (!wb.Worksheets.TryGetWorksheet("Questions", out var ws))
            ws = wb.Worksheets.Worksheet(1); // fallback nếu user đổi tên

        var range = ws.RangeUsed();
        if (range == null) return res;

        // Chuẩn hoá & key
        static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
        static string MakeKey(string content, QType type, string skill)
            => $"{Norm(content)}|{type}|{Norm(skill)}";

        // Load keys hiện có để chống trùng
        var existing = await _qRepo.GetAllAsync();
        var existingKeys = existing
            .Select(q => MakeKey(q.Content, q.Type, q.Skill))
            .ToHashSet(StringComparer.Ordinal);

        var seenInThisFile = new HashSet<string>(StringComparer.Ordinal);

        // Tìm index cột theo header (linh hoạt theo tên)
        var headerRow = range.FirstRow();
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c <= headerRow.CellCount(); c++)
        {
            var name = headerRow.Cell(c).GetString().Trim();
            if (!string.IsNullOrEmpty(name))
                colIndex[name] = c;
        }

        int GetCol(string name, bool required = false)
        {
            if (colIndex.TryGetValue(name, out var c)) return c;
            if (required) throw new Exception($"Missing required column: {name}");
            return -1;
        }

        int cContent = GetCol("Content", required: true);
        int cType = GetCol("Type", required: true);
        int cSkill = GetCol("Skill", required: true);
        int cDifficulty = GetCol("Difficulty");
        int cTags = GetCol("Tags (comma)");
        int cOptions = GetCol("Options (|)");
        int cCorrect = GetCol("CorrectKeys (|)");
        int cEssayMin = GetCol("EssayMinWords");
        int cPairs = GetCol("MatchingPairs (L=R || L=R)");
        int cDragTokens = GetCol("DragTokens (|)");
        int cDragSlots = GetCol("DragSlots (Name=Answer || Name=Answer)");
        int cMedia = GetCol("Media (FileName#Url#Caption || ...)");

        foreach (var row in range.RowsUsed().Skip(1))
        {
            res.Total++;
            try
            {
                var content = row.Cell(cContent).GetString().Trim();
                var typeRaw = row.Cell(cType).GetString().Trim();
                var skill = row.Cell(cSkill).GetString().Trim();

                if (string.IsNullOrWhiteSpace(content))
                    throw new Exception("Content is empty");
                if (string.IsNullOrWhiteSpace(typeRaw))
                    throw new Exception("Type is empty");
                if (!Enum.TryParse<QType>(typeRaw, true, out var type))
                    throw new Exception($"Invalid Type: {typeRaw}");
                if (string.IsNullOrWhiteSpace(skill))
                    throw new Exception("Skill is empty");

                var key = MakeKey(content, type, skill);

                // Chống trùng (DB + ngay trong file)
                if (existingKeys.Contains(key) || seenInThisFile.Contains(key))
                {
                    res.Skipped++;
                    res.SkippedReasons.Add($"Row {row.RowNumber()}: skipped duplicate (Content,Type,Skill).");
                    continue;
                }

                // Các field còn lại
                var difficulty = cDifficulty > 0 ? row.Cell(cDifficulty).GetString().Trim() : null;
                var tagsCsv = cTags > 0 ? row.Cell(cTags).GetString() : "";
                var optionsStr = cOptions > 0 ? row.Cell(cOptions).GetString() : "";
                var correctStr = cCorrect > 0 ? row.Cell(cCorrect).GetString() : "";
                var essayMin = cEssayMin > 0 ? row.Cell(cEssayMin).GetString().Trim() : "";

                var q = new Question
                {
                    Content = content,
                    Type = type,
                    Skill = skill,
                    Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "Junior" : difficulty,
                    Tags = SplitCsv(tagsCsv, ','),
                    Options = SplitCsv(optionsStr, '|'),
                    CorrectKeys = SplitCsv(correctStr, '|'),
                    EssayMinWords = ParseNullableInt(essayMin)
                };

                // MatchingPairs
                if (cPairs > 0)
                {
                    var pairsStr = row.Cell(cPairs).GetString();
                    var pairs = new List<MatchPair>();
                    foreach (var token in SplitRaw(pairsStr, "||"))
                    {
                        var eqIdx = token.IndexOf('=', StringComparison.Ordinal);
                        if (eqIdx <= 0) continue;
                        var left = token[..eqIdx].Trim();
                        var right = token[(eqIdx + 1)..].Trim();
                        if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
                            pairs.Add(new MatchPair(left, right));
                    }
                    if (pairs.Count > 0) q.MatchingPairs = pairs;
                }

                // DragDrop
                var dragTokens = cDragTokens > 0 ? SplitCsv(row.Cell(cDragTokens).GetString(), '|') : new List<string>();
                var dragSlots = new List<DragSlot>();
                if (cDragSlots > 0)
                {
                    var slotsStr = row.Cell(cDragSlots).GetString();
                    foreach (var token in SplitRaw(slotsStr, "||"))
                    {
                        var eqIdx = token.IndexOf('=', StringComparison.Ordinal);
                        if (eqIdx <= 0) continue;
                        var name = token[..eqIdx].Trim();
                        var ans = token[(eqIdx + 1)..].Trim();
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(ans))
                            dragSlots.Add(new DragSlot(name, ans));
                    }
                }
                if (dragTokens.Count > 0 || dragSlots.Count > 0)
                {
                    q.DragDrop = new DragDropConfig
                    {
                        Tokens = dragTokens,
                        Slots = dragSlots
                    };
                }

                // Media
                if (cMedia > 0)
                {
                    var mediaStr = row.Cell(cMedia).GetString();
                    var items = new List<MediaFile>();
                    foreach (var piece in SplitRaw(mediaStr, "||"))
                    {
                        var parts = piece.Split('#');
                        if (parts.Length >= 2)
                        {
                            items.Add(new MediaFile
                            {
                                FileName = parts[0].Trim(),
                                Url = parts[1].Trim(),
                                Caption = parts.Length >= 3 ? parts[2].Trim() : null
                            });
                        }
                    }
                    if (items.Count > 0) q.Media = items;
                }

                // Đặc thù True/False
                if (type == QType.TrueFalse)
                {
                    q.Options = new() { "True", "False" };
                    // CorrectKeys phải là đúng 1 trong hai giá trị
                    if (q.CorrectKeys == null || q.CorrectKeys.Count != 1 ||
                        !(q.CorrectKeys[0].Equals("True", StringComparison.OrdinalIgnoreCase) ||
                          q.CorrectKeys[0].Equals("False", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new Exception("TrueFalse requires CorrectKeys to be exactly one of True/False.");
                    }
                    // Chuẩn hoá chữ hoa đầu cho nhất quán
                    q.CorrectKeys = new() { q.CorrectKeys[0].Equals("true", StringComparison.OrdinalIgnoreCase) ? "True" : "False" };
                }

                // Lưu — để QuestionService.ValidateQuestion lo kiểm tra theo Type
                await _svc.CreateAsync(q, actor);

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

    // --------------- Helpers ---------------
    private static int? ParseNullableInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static List<string> SplitCsv(string? s, char sep)
        => string.IsNullOrWhiteSpace(s)
            ? new List<string>()
            : s.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static List<string> SplitRaw(string? s, string delimiter)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        foreach (var part in s.Split(delimiter, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim();
            if (!string.IsNullOrEmpty(p)) list.Add(p);
        }
        return list;
    }
}
