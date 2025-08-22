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
    [HttpGet] public IActionResult Create() => View(new Question { Type = QType.MCQ, Options = new() { "A", "B", "C", "D" }, CorrectKeys = new() { "A" } });

    [HttpPost]
    public async Task<IActionResult> Create(Question q, List<IFormFile>? mediaFiles)
    {
        try
        {
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
    public async Task<IActionResult> Edit(Question q, List<IFormFile>? mediaFiles)
    {
        if (!ModelState.IsValid)
            return View(q);

        try
        {
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
        if (file == null || file.Length == 0) { TempData["Err"] = "Chọn file Excel"; return RedirectToAction(nameof(Index)); }
        using var s = file.OpenReadStream();
        var res = await _xlsx.ImportAsync(s, User.Identity?.Name ?? "hr");
        TempData["Msg"] = $"Imported {res.Success}/{res.Total}. Errors: {res.Errors.Count}";
        if (res.Errors.Any()) TempData["ErrDetail"] = string.Join("\n", res.Errors);
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
}
