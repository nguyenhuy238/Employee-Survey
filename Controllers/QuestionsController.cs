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
    private readonly IRepository<Question> _qRepo;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _cfg;

    public QuestionsController(
        Application.IQuestionService svc,
        IQuestionExcelService xlsx,
        IRepository<Question> qRepo,
        IWebHostEnvironment env,
        IConfiguration cfg)
    { _svc = svc; _xlsx = xlsx; _qRepo = qRepo; _env = env; _cfg = cfg; }

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
        string? CorrectKeys,
        string? MatchingPairsRaw,
        string? DragTokens,
        string? DragSlotsRaw,
        string? TagsCsv)
    {
        try
        {
            NormalizeQuestionFieldsFromForm(q, CorrectKeys, MatchingPairsRaw, DragTokens, DragSlotsRaw, TagsCsv);

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

    // ===== EDIT (ROUTES RÕ RÀNG) =====
    // GET: /Questions/Edit/{id}
    [HttpGet("/Questions/Edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var q = await _svc.GetAsync(id);
        if (q == null) return NotFound();
        return View(q);
    }

    // POST: /Questions/Edit/{id}
    [HttpPost("/Questions/Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,                     // id từ route
        Question q,                    // dữ liệu post từ form
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
            // đảm bảo q.Id có giá trị (từ hidden hoặc route)
            if (string.IsNullOrWhiteSpace(q.Id)) q.Id = id;

            var original = await _svc.GetAsync(q.Id);
            if (original == null)
            {
                ModelState.AddModelError("", "Not found");
                return View(q);
            }

            NormalizeQuestionFieldsFromForm(q, CorrectKeys, MatchingPairsRaw, DragTokens, DragSlotsRaw, TagsCsv);

            // giữ audit cũ
            q.CreatedAt = original.CreatedAt;
            q.CreatedBy = original.CreatedBy;

            // merge media cũ + mới
            var mergedMedia = (original.Media ?? new List<MediaFile>()).ToList();
            if (mediaFiles?.Any() == true)
            {
                var newly = await SaveMediaAsync(mediaFiles);
                var exists = new HashSet<string>(mergedMedia.Select(m => m.Url), StringComparer.OrdinalIgnoreCase);
                foreach (var m in newly) if (exists.Add(m.Url)) mergedMedia.Add(m);
            }
            q.Media = mergedMedia;

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
    // ===== END EDIT =====

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

    // DELETE QUESTION
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

    // DELETE 1 MEDIA
    [HttpPost]
    public async Task<IActionResult> DeleteMedia(string questionId, string mediaId)
    {
        var q = await _svc.GetAsync(questionId);
        if (q == null) return NotFound();

        var m = q.Media.FirstOrDefault(x => x.Id == mediaId);
        if (m == null) return RedirectToAction(nameof(Edit), new { id = questionId });

        try
        {
            var physical = Path.Combine(_env.WebRootPath, m.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physical))
                System.IO.File.Delete(physical);
        }
        catch { /* ignore */ }

        q.Media.RemoveAll(x => x.Id == mediaId);
        var (ok, reason) = await _svc.UpdateAsync(q, User.Identity?.Name ?? "hr");
        if (!ok) TempData["Err"] = reason ?? "Không thể xóa file";

        return RedirectToAction(nameof(Edit), new { id = questionId });
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
        if (q.Options != null)
        {
            q.Options = q.Options
                .SelectMany(line => (line ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(correctKeys))
            q.CorrectKeys = correctKeys.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (!string.IsNullOrWhiteSpace(tagsCsv))
            q.Tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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

        var allowed = _cfg.GetSection("AllowedUploadContentTypes").Get<string[]>() ?? new[]
        {
            "image/jpeg","image/png","image/gif","image/webp",
            "audio/mpeg","audio/wav","audio/ogg",
            "video/mp4",
            "application/pdf"
        };
        var maxBytes = _cfg.GetValue<long?>("MaxUploadFileSizeBytes") ?? 10L * 1024 * 1024;

        foreach (var f in files)
        {
            if (f.Length == 0) continue;
            if (f.Length > maxBytes)
                throw new InvalidOperationException($"File {f.FileName} vượt quá {(maxBytes / (1024 * 1024))}MB.");

            var contentType = string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType;
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();

            bool okType =
                allowed.Contains(contentType)
                || (allowed.Any(a => a.StartsWith("image/")) && (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"))
                || (allowed.Contains("application/pdf") && ext == ".pdf")
                || (allowed.Any(a => a.StartsWith("audio/")) && (ext is ".mp3" or ".wav" or ".ogg"))
                || (allowed.Any(a => a.StartsWith("video/")) && (ext is ".mp4"));

            if (!okType)
                throw new InvalidOperationException($"Loại file {f.FileName} ({contentType}) không được phép.");

            var name = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(root, name);

            using var fs = System.IO.File.Create(path);
            await f.CopyToAsync(fs);

            result.Add(new MediaFile
            {
                FileName = f.FileName,
                Url = $"/uploads/{name}",
                ContentType = contentType,
                Size = f.Length
            });
        }
        return result;
    }
}
