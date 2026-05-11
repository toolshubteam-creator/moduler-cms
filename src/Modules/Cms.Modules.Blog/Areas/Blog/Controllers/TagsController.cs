namespace Cms.Modules.Blog.Areas.Blog.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Blog.Areas.Blog.ViewModels;
using Cms.Modules.Blog.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Blog")]
[Authorize]
public sealed class TagsController(ITagService tags) : Controller
{
    [HttpGet]
    [HasPermission("blog.tags.view")]
    public async Task<IActionResult> Index()
    {
        var list = await tags.ListAsync(HttpContext.RequestAborted);
        return View(list);
    }

    [HttpGet]
    [HasPermission("blog.tags.create")]
    public IActionResult Create() => View(new TagFormModel());

    [HttpPost]
    [HasPermission("blog.tags.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TagFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var dto = await tags.CreateAsync(new CreateTagRequest(model.Name, model.Slug), HttpContext.RequestAborted);
            TempData["SuccessMessage"] = $"Etiket '{dto.Name}' olusturuldu (slug={dto.Slug}).";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    [HasPermission("blog.tags.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await tags.GetAsync(id, HttpContext.RequestAborted);
        if (dto is null)
        {
            return NotFound();
        }
        return View(new TagFormModel { Id = dto.Id, Name = dto.Name, Slug = dto.Slug });
    }

    [HttpPost]
    [HasPermission("blog.tags.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TagFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var dto = await tags.UpdateAsync(
                new UpdateTagRequest(model.Id, model.Name, model.Slug ?? model.Name),
                HttpContext.RequestAborted);
            TempData["SuccessMessage"] = $"Etiket '{dto.Name}' guncellendi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [HasPermission("blog.tags.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await tags.DeleteAsync(id, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Etiket silindi (hard delete; junction kayitlari da silindi).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
