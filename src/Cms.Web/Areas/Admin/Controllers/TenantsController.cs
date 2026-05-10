namespace Cms.Web.Areas.Admin.Controllers;

using Cms.Core.Tenancy;
using Cms.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Admin")]
[Authorize(Policy = "SystemRole")]
public sealed class TenantsController(ITenantProvisioningService provisioning) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool includeInactive = false)
    {
        var tenants = await provisioning.ListAsync(includeInactive, HttpContext.RequestAborted);
        ViewData["IncludeInactive"] = includeInactive;
        return View(tenants);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateTenantViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await provisioning.CreateAsync(
            new CreateTenantRequest(model.Slug, model.DisplayName),
            HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Tenant olusturulamadi.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Tenant '{result.Tenant!.Slug}' olusturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var ok = await provisioning.DeactivateAsync(id, HttpContext.RequestAborted);
        if (!ok)
        {
            TempData["ErrorMessage"] = "Tenant bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Tenant pasiflestirildi.";
        return RedirectToAction(nameof(Index));
    }
}
