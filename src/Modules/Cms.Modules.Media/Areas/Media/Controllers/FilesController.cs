namespace Cms.Modules.Media.Areas.Media.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Media.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[Area("Media")]
[Authorize]
[HasPermission("media.files.view")]
public sealed class FilesController(IMediaService media) : Controller
{
    private const long MaxUploadBytes = 50_000_000;

    [HttpGet]
    public async Task<IActionResult> Index(int skip = 0, int take = 50)
    {
        var files = await media.ListAsync(skip, take, HttpContext.RequestAborted);
        ViewData["Skip"] = skip;
        ViewData["Take"] = take;
        return View(files);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var file = await media.GetByIdAsync(id, HttpContext.RequestAborted);
        if (file is null)
        {
            return NotFound();
        }
        return View(file);
    }

    [HttpGet]
    public async Task<IActionResult> Content(int id)
    {
        var file = await media.GetByIdAsync(id, HttpContext.RequestAborted);
        if (file is null)
        {
            return NotFound();
        }
        var stream = await media.OpenContentAsync(id, HttpContext.RequestAborted);
        if (stream is null)
        {
            return NotFound();
        }
        Response.Headers.ETag = $"\"{file.Hash}\"";
        return File(stream, file.MimeType, file.FileName);
    }

    [HttpPost]
    [HasPermission("media.files.upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, string? altText)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Dosya secilmedi veya bos.";
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > MaxUploadBytes)
        {
            TempData["ErrorMessage"] = $"Dosya {MaxUploadBytes / 1_000_000} MB sinirini asti.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var saved = await media.UploadAsync(
            stream,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            altText,
            HttpContext.RequestAborted);

        TempData["SuccessMessage"] = $"'{saved.FileName}' yuklendi (id={saved.Id}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("media.files.upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string? altText)
    {
        await media.UpdateMetadataAsync(id, altText, HttpContext.RequestAborted);
        TempData["SuccessMessage"] = "Metadata guncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [HasPermission("media.files.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await media.DeleteAsync(id, HttpContext.RequestAborted);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? "Dosya silindi (soft-delete, geri yuklenebilir)."
            : "Dosya bulunamadi.";
        return RedirectToAction(nameof(Index));
    }
}
