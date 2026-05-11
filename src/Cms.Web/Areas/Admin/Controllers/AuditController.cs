namespace Cms.Web.Areas.Admin.Controllers;

using Cms.Core.Data;
using Cms.Core.Domain.Auditing;
using Cms.Core.Tenancy;
using Cms.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Area("Admin")]
[Authorize(Policy = "SystemRole")]
// Faz-7'de tenant-scoped permission ile aktive edilecek:
// [HasPermission(CorePermissions.AuditView.Key)] — su an /admin path'i TenantResolutionMiddleware
// tarafindan bypass edildigi icin permission handler tenantId=null ile global scope'ta kontrol
// ederdi. SystemRole policy zaten cross-tenant admin'i koruyor.
public sealed class AuditController(
    ITenantProvisioningService provisioning,
    ITenantDbContextFactory tenantFactory) : Controller
{
    private const int PageSize = 50;

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? tenantId,
        string? entityName,
        int? userId,
        [FromQuery(Name = "act")] AuditAction? auditAction,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        if (page < 1)
        {
            page = 1;
        }

        var tenants = await provisioning.ListAsync(includeInactive: true, HttpContext.RequestAborted);
        var model = new AuditIndexViewModel
        {
            Tenants = [.. tenants.Select(t => new TenantOption(t.Id, t.Slug, t.DisplayName))],
            SelectedTenantId = tenantId,
            Filter = new AuditFilterViewModel
            {
                EntityName = entityName,
                UserId = userId,
                Action = auditAction,
                From = from,
                To = to,
            },
            Page = page,
            PageSize = PageSize,
        };

        if (tenantId is null)
        {
            return View(model);
        }

        var tenant = await provisioning.GetByIdAsync(tenantId.Value, HttpContext.RequestAborted);
        if (tenant is null)
        {
            ModelState.AddModelError(string.Empty, "Tenant bulunamadi.");
            model.SelectedTenantId = null;
            return View(model);
        }

        await using var tenantCtx = tenantFactory.Create(tenant.ConnectionString);
        var query = tenantCtx.Set<AuditEntry>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var en = entityName.Trim();
            query = query.Where(a => a.EntityName.Contains(en));
        }
        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }
        if (auditAction.HasValue)
        {
            query = query.Where(a => a.Action == auditAction.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(a => a.Timestamp >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(a => a.Timestamp <= to.Value);
        }

        model.TotalCount = await query.CountAsync(HttpContext.RequestAborted);
        model.Entries = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(HttpContext.RequestAborted);

        return View(model);
    }
}
