namespace Cms.Modules.Seo.Areas.Seo.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Seo.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Seo")]
[Authorize]
[HasPermission("seo.metas.view")]
public sealed class MetasController(ISeoMetaService seo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int skip = 0, int take = 50)
    {
        var entries = await seo.ListAsync(skip, take, HttpContext.RequestAborted);
        ViewData["Skip"] = skip;
        ViewData["Take"] = take;
        return View(entries);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string targetType, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetId))
        {
            return BadRequest();
        }
        var meta = await seo.GetAsync(targetType, targetId, HttpContext.RequestAborted);
        if (meta is null)
        {
            return NotFound();
        }
        return View(meta);
    }

    [HttpGet]
    [HasPermission("seo.metas.edit")]
    public async Task<IActionResult> Edit(string? targetType, string? targetId)
    {
        var model = new EditViewModel
        {
            TargetType = targetType ?? string.Empty,
            TargetId = targetId ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(targetType) && !string.IsNullOrWhiteSpace(targetId))
        {
            var existing = await seo.GetAsync(targetType, targetId, HttpContext.RequestAborted);
            if (existing is not null)
            {
                model.Title = existing.Title;
                model.Description = existing.Description;
                model.OgImage = existing.OgImage;
                model.Canonical = existing.Canonical;
                model.Robots = existing.Robots;
            }
        }

        return View(model);
    }

    [HttpPost]
    [HasPermission("seo.metas.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.TargetType) || string.IsNullOrWhiteSpace(model.TargetId))
        {
            ModelState.AddModelError(string.Empty, "TargetType ve TargetId bos olamaz.");
            return View(model);
        }

        var input = new SeoMetaInput(model.Title, model.Description, model.OgImage, model.Canonical, model.Robots);
        await seo.SetAsync(model.TargetType, model.TargetId, input, HttpContext.RequestAborted);

        TempData["SuccessMessage"] = $"'{model.TargetType}/{model.TargetId}' kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("seo.metas.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string targetType, string targetId)
    {
        var ok = await seo.DeleteAsync(targetType, targetId, HttpContext.RequestAborted);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? $"'{targetType}/{targetId}' silindi."
            : $"'{targetType}/{targetId}' bulunamadi.";
        return RedirectToAction(nameof(Index));
    }

    public sealed class EditViewModel
    {
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? OgImage { get; set; }
        public string? Canonical { get; set; }
        public string? Robots { get; set; }
    }
}
