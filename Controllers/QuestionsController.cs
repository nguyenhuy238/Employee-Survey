using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers;

[Authorize(Roles = "Admin,HR")]
public class QuestionsController : Controller
{
    private readonly Application.IQuestionService _svc;
    private readonly IQuestionExcelService _xlsx;
    private readonly IRepository<Question> _qRepo; // for export all quick
    private readonly IWebHostEnvironment _env;

    public QuestionsController(Application.IQuestionService svc, IQuestionExcelService xlsx, IRepository<Question> qRepo, IWebHostEnvironment env)
    { _svc = svc; _xlsx = xlsx; _qRepo = qRepo; _env = env; }

    // LIST + FILTER + PAGING
    public async Task<IActionResult> Index([FromQuery] QuestionFilter f)
    {
        var result = await _svc.SearchAsync(f);
        return View(result);
    }

    // CREATE
    [HttpGet]
    public IActionResult Create()
        => View(new Question { Type = QType.MCQ, Options = new() { "A", "B", "C", "D" }, CorrectKeys = new() { "A" } });

    [HttpPost]
    public async Task<IActionResult> Create(
        Question q,
        List<IFormFile>? mediaFiles,
        // các field từ form partial (_Form.cshtml)
        string? CorrectKeys,
        string? MatchingPairsRaw,
        string? DragTokens,
        string? DragSlotsRaw,
        string? TagsCsv)
    {
        try
        {
            // Chuẩn hoá field theo Type
            NormalizeQuestionFieldsFromForm(q, CorrectKeys, MatchingPairsRaw, DragTokens, DragSlotsRaw, TagsCsv);

            // upload media (optional)
            if (mediaFiles?.Any() == true)
                q.Media = await SaveMediaAsync(mediaFiles);

            var id = await _svc.CreateAsync(q, User.Identity?.Name ?? "hr");
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(q);
        }
    }

    // EDIT
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var q = await _svc.GetAsync(id);
        if (q == null) return NotFound();
        return View(q);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        Question q,
        List<IFormFile>? mediaFiles,
        string? CorrectKeys,
        string? MatchingPairsRaw,
        string? DragTokens,
        string? DragSlotsRaw,
        string? TagsCsv)
    {
        if (!ModelState.IsValid)
            return View(q);

        try
        {
            NormalizeQuestionFieldsFromForm(q, CorrectKeys, MatchingPairsRaw, DragTokens, DragSlotsRaw, TagsCsv);

            // Nếu có file media mới, upload và cập nhật
            if (mediaFiles?.Any() == true)
                q.Media = await SaveMediaAsync(mediaFiles);

            var (success, reason) = await _svc.UpdateAsync(q, User.Identity?.Name ?? "hr");
            if (!success)
            {
                ModelState.AddModelError("", reason ?? "Update failed");
                return View(q);
            }
            TempData["Msg"] = "Cập nhật thành công";
            return RedirectToAction(nameof(Edit), new { id = q.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(q);
        }
    }

    public IActionResult DetailsBlocked(string id, string reason) { ViewBag.Reason = reason; ViewBag.Id = id; return View(); }

    // CLONE
    [HttpPost]
    public async Task<IActionResult> Clone(string id)
    {
        var newId = await _svc.CloneAsync(id, User.Identity?.Name ?? "hr");
        return RedirectToAction(nameof(Edit), new { id = newId });
    }

    // IMPORT
    [HttpPost]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Err"] = "Chọn file Excel";
            return RedirectToAction(nameof(Index));
        }
        using var s = file.OpenReadStream();
        var res = await _xlsx.ImportAsync(s, User.Identity?.Name ?? "hr");

        // cập nhật thông báo có Skipped
        TempData["Msg"] = $"Imported {res.Success}/{res.Total}. Skipped: {res.Skipped}. Errors: {res.Errors.Count}";

        if (res.Errors.Any() || res.SkippedReasons.Any())
            TempData["ErrDetail"] = string.Join("\n", res.Errors.Concat(res.SkippedReasons));

        return RedirectToAction(nameof(Index));
    }

    // EXPORT
    [HttpGet]
    public async Task<FileResult> ExportExcel()
    {
        var all = await _qRepo.GetAllAsync();
        var bytes = await _xlsx.ExportAsync(all);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "QuestionBank.xlsx");
    }

    // DELETE
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var (success, reason) = await _svc.DeleteAsync(id, User.Identity?.Name ?? "hr");
        if (!success)
        {
            TempData["Err"] = reason ?? "Xóa thất bại";
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData["Msg"] = "Đã xóa câu hỏi";
        return RedirectToAction(nameof(Index));
    }

    // Helpers
    private static void NormalizeQuestionFieldsFromForm(
        Question q,
        string? correctKeys,
        string? matchingPairsRaw,
        string? dragTokens,
        string? dragSlotsRaw,
        string? tagsCsv)
    {
        // Options: textarea mỗi dòng 1 option -> đã bind vào q.Options nếu name="Options"
        if (q.Options != null)
        {
            // làm sạch khoảng trắng/thừa dòng
            q.Options = q.Options
                .SelectMany(line => (line ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        // CorrectKeys: input kiểu "A|C" hoặc "True"
        if (!string.IsNullOrWhiteSpace(correctKeys))
            q.CorrectKeys = correctKeys.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Tags
        if (!string.IsNullOrWhiteSpace(tagsCsv))
            q.Tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Matching
        if (q.Type == QType.Matching)
        {
            q.MatchingPairs = new();
            var lines = (matchingPairsRaw ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var parts = raw.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                    q.MatchingPairs.Add(new MatchPair(parts[0], parts[1]));
            }
        }

        // DragDrop
        if (q.Type == QType.DragDrop)
        {
            var tokens = string.IsNullOrWhiteSpace(dragTokens)
                ? new List<string>()
                : dragTokens.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var slots = new List<DragSlot>();
            var lines = (dragSlotsRaw ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var parts = raw.Split('=', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    slots.Add(new DragSlot(parts[0], parts[1]));
            }
            q.DragDrop = new DragDropConfig { Tokens = tokens, Slots = slots };
        }

        // Chuẩn hoá True/False (đảm bảo Options đúng)
        if (q.Type == QType.TrueFalse)
        {
            q.Options = new() { "True", "False" };
        }
    }

    private async Task<List<MediaFile>> SaveMediaAsync(List<IFormFile> files)
    {
        var result = new List<MediaFile>();
        var root = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(root);
        foreach (var f in files)
        {
            var safe = Path.GetFileNameWithoutExtension(f.FileName);
            safe = string.Join("_", safe.Split(Path.GetInvalidFileNameChars()));
            var ext = Path.GetExtension(f.FileName);
            var name = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(root, name);
            using var fs = System.IO.File.Create(path);
            await f.CopyToAsync(fs);
            result.Add(new MediaFile { FileName = f.FileName, Url = $"/uploads/{name}", ContentType = f.ContentType, Size = f.Length });
        }
        return result;
    }


}
