namespace Cms.Modules.Blog.Areas.Blog.Controllers;

using System.Globalization;
using System.Security.Claims;
using Cms.Core.Authorization;
using Cms.Modules.Blog.Areas.Blog.ViewModels;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Media.Contracts;
using Cms.Modules.Seo.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Blog")]
[Authorize]
public sealed class PostsController(
    IPostService posts,
    IMediaService mediaService,
    ISeoMetaService seoService) : Controller
{
    private const string SeoTargetType = "blog.post";

    [HttpGet]
    [HasPermission("blog.posts.view")]
    public async Task<IActionResult> Index(int skip = 0, int take = 50)
    {
        var list = await posts.ListAsync(skip, take, HttpContext.RequestAborted);
        ViewData["Skip"] = skip;
        ViewData["Take"] = take;
        return View(list);
    }

    [HttpGet]
    [HasPermission("blog.posts.create")]
    public async Task<IActionResult> Create()
    {
        ViewData["MediaList"] = await mediaService.ListAsync(0, 100, HttpContext.RequestAborted);
        return View(new PostFormModel());
    }

    [HttpPost]
    [HasPermission("blog.posts.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PostFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            ViewData["MediaList"] = await mediaService.ListAsync(0, 100, HttpContext.RequestAborted);
            return View(model);
        }

        var authorId = ResolveCurrentUserId();
        var dto = await posts.CreateAsync(new CreatePostRequest(
            model.Title,
            model.Slug,
            model.Excerpt,
            model.Content,
            model.Status,
            model.PublishAt,
            model.FeaturedMediaId,
            authorId),
            HttpContext.RequestAborted);

        await SaveSeoIfPresentAsync(dto.Id, model);

        TempData["SuccessMessage"] = $"Post '{dto.Title}' olusturuldu (slug={dto.Slug}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [HasPermission("blog.posts.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await posts.GetAsync(id, HttpContext.RequestAborted);
        if (dto is null)
        {
            return NotFound();
        }

        var seo = await seoService.GetAsync(SeoTargetType, id.ToString(CultureInfo.InvariantCulture), HttpContext.RequestAborted);

        ViewData["MediaList"] = await mediaService.ListAsync(0, 100, HttpContext.RequestAborted);
        return View(new PostFormModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Slug = dto.Slug,
            Excerpt = dto.Excerpt,
            Content = dto.Content,
            Status = dto.Status,
            PublishAt = dto.PublishAt,
            FeaturedMediaId = dto.FeaturedMediaId,
            SeoTitle = seo?.Title,
            SeoDescription = seo?.Description,
            SeoOgImage = seo?.OgImage,
            SeoCanonical = seo?.Canonical,
            SeoRobots = seo?.Robots,
        });
    }

    [HttpPost]
    [HasPermission("blog.posts.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PostFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            ViewData["MediaList"] = await mediaService.ListAsync(0, 100, HttpContext.RequestAborted);
            return View(model);
        }

        var dto = await posts.UpdateAsync(new UpdatePostRequest(
            model.Id,
            model.Title,
            string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug,
            model.Excerpt,
            model.Content,
            model.Status,
            model.PublishAt,
            model.FeaturedMediaId),
            HttpContext.RequestAborted);

        if (HasSeoInput(model))
        {
            await SaveSeoIfPresentAsync(dto.Id, model);
        }
        else
        {
            // Form'da SEO tum alanlari bos -> kayitli SEO meta varsa sil.
            await seoService.DeleteAsync(SeoTargetType, dto.Id.ToString(CultureInfo.InvariantCulture), HttpContext.RequestAborted);
        }

        TempData["SuccessMessage"] = $"Post '{dto.Title}' guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("blog.posts.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await posts.DeleteAsync(id, HttpContext.RequestAborted);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? "Post silindi (soft-delete, geri yuklenebilir)."
            : "Post bulunamadi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("blog.posts.publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        await posts.PublishAsync(id, HttpContext.RequestAborted);
        TempData["SuccessMessage"] = "Post yayinlandi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("blog.posts.publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id)
    {
        await posts.UnpublishAsync(id, HttpContext.RequestAborted);
        TempData["SuccessMessage"] = "Post draft'a alindi (yayin gecmisi PublishedAt korunur).";
        return RedirectToAction(nameof(Index));
    }

    private async Task SaveSeoIfPresentAsync(int postId, PostFormModel model)
    {
        if (!HasSeoInput(model))
        {
            return;
        }
        await seoService.SetAsync(
            SeoTargetType,
            postId.ToString(CultureInfo.InvariantCulture),
            new SeoMetaInput(
                model.SeoTitle,
                model.SeoDescription,
                model.SeoOgImage,
                model.SeoCanonical,
                model.SeoRobots),
            HttpContext.RequestAborted);
    }

    private static bool HasSeoInput(PostFormModel m) =>
        !string.IsNullOrWhiteSpace(m.SeoTitle) ||
        !string.IsNullOrWhiteSpace(m.SeoDescription) ||
        !string.IsNullOrWhiteSpace(m.SeoOgImage) ||
        !string.IsNullOrWhiteSpace(m.SeoCanonical) ||
        !string.IsNullOrWhiteSpace(m.SeoRobots);

    private int ResolveCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
    }
}
