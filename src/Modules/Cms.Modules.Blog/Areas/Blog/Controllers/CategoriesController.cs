namespace Cms.Modules.Blog.Areas.Blog.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Blog.Areas.Blog.ViewModels;
using Cms.Modules.Blog.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Blog")]
[Authorize]
public sealed class CategoriesController(ICategoryService categories) : Controller
{
    [HttpGet]
    [HasPermission("blog.categories.view")]
    public async Task<IActionResult> Index()
    {
        var tree = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
        return View(tree);
    }

    [HttpGet]
    [HasPermission("blog.categories.create")]
    public async Task<IActionResult> Create()
    {
        ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
        return View(new CategoryFormModel());
    }

    [HttpPost]
    [HasPermission("blog.categories.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
            return View(model);
        }

        try
        {
            var dto = await categories.CreateAsync(
                new CreateCategoryRequest(model.Name, model.Slug, model.Description, model.ParentCategoryId),
                HttpContext.RequestAborted);
            TempData["SuccessMessage"] = $"Kategori '{dto.Name}' olusturuldu (slug={dto.Slug}).";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
            return View(model);
        }
    }

    [HttpGet]
    [HasPermission("blog.categories.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await categories.GetAsync(id, HttpContext.RequestAborted);
        if (dto is null)
        {
            return NotFound();
        }
        ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
        return View(new CategoryFormModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            ParentCategoryId = dto.ParentCategoryId,
        });
    }

    [HttpPost]
    [HasPermission("blog.categories.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
            return View(model);
        }

        try
        {
            var dto = await categories.UpdateAsync(
                new UpdateCategoryRequest(model.Id, model.Name, model.Slug ?? model.Name, model.Description, model.ParentCategoryId),
                HttpContext.RequestAborted);
            TempData["SuccessMessage"] = $"Kategori '{dto.Name}' guncellendi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ParentOptions"] = await categories.ListWithIndentAsync(HttpContext.RequestAborted);
            return View(model);
        }
    }

    [HttpPost]
    [HasPermission("blog.categories.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await categories.DeleteAsync(id, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Kategori silindi (soft-delete, geri yuklenebilir).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
