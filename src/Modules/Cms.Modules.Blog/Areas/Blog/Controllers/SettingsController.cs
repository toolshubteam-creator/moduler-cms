namespace Cms.Modules.Blog.Areas.Blog.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Blog.Areas.Blog.ViewModels;
using Cms.Modules.Blog.Services;
using Cms.Modules.Settings.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Blog")]
[Authorize]
public sealed class SettingsController(
    ISettingsService settings,
    IBlogSettingsReader reader) : Controller
{
    [HttpGet]
    [HasPermission("blog.settings.view")]
    public async Task<IActionResult> Index()
    {
        var snap = await reader.GetAsync(HttpContext.RequestAborted);
        return View(new BlogSettingsFormModel
        {
            UrlPattern = snap.UrlPattern,
            PostsPerPage = snap.PostsPerPage,
            DefaultMetaTitle = snap.DefaultMetaTitle,
            DefaultMetaDescription = snap.DefaultMetaDescription,
            ShowExcerptInList = snap.ShowExcerptInList,
        });
    }

    [HttpPost]
    [HasPermission("blog.settings.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BlogSettingsFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ct = HttpContext.RequestAborted;
        await settings.SetAsync(BlogSettingKeys.UrlPattern, model.UrlPattern, ct);
        await settings.SetAsync(BlogSettingKeys.PostsPerPage, model.PostsPerPage, ct);
        await settings.SetAsync(BlogSettingKeys.DefaultMetaTitle, model.DefaultMetaTitle ?? string.Empty, ct);
        await settings.SetAsync(BlogSettingKeys.DefaultMetaDescription, model.DefaultMetaDescription ?? string.Empty, ct);
        await settings.SetAsync(BlogSettingKeys.ShowExcerptInList, model.ShowExcerptInList, ct);

        TempData["SuccessMessage"] = "Blog ayarlari guncellendi.";
        return RedirectToAction(nameof(Index));
    }
}
