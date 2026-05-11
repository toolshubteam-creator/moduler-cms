namespace Cms.Web.Areas.Admin.Controllers;

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Cms.Core.Data;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Tenancy;
using Cms.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Area("Admin")]
[Authorize(Policy = "SystemRole")]
public sealed class SoftDeleteController(
    ITenantProvisioningService provisioning,
    ITenantDbContextFactory tenantFactory,
    ModuleDescriptorRegistry registry) : Controller
{
    private const int PageSize = 50;

    [HttpGet]
    public async Task<IActionResult> Index(Guid? tenantId, string? entityName, int page = 1)
    {
        if (page < 1)
        {
            page = 1;
        }

        var tenants = await provisioning.ListAsync(includeInactive: false, HttpContext.RequestAborted);
        var entityTypes = registry.GetSoftDeletableEntityTypes()
            .Select(t => t.Name)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var model = new SoftDeleteIndexViewModel
        {
            Tenants = [.. tenants.Select(t => new TenantOption(t.Id, t.Slug, t.DisplayName))],
            SelectedTenantId = tenantId,
            EntityTypeOptions = entityTypes,
            SelectedEntityName = entityName,
            Page = page,
            PageSize = PageSize,
        };

        if (tenantId is null || string.IsNullOrWhiteSpace(entityName))
        {
            return View(model);
        }

        var clrType = ResolveEntityType(entityName);
        if (clrType is null)
        {
            ModelState.AddModelError(string.Empty, $"Bilinmeyen entity tipi: {entityName}");
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
        var (entries, totalCount) = await QueryDeletedAsync(tenantCtx, clrType, (page - 1) * PageSize, PageSize, HttpContext.RequestAborted);

        model.Entries = entries;
        model.TotalCount = totalCount;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid tenantId, string entityName, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(entityId))
        {
            TempData["ErrorMessage"] = "Eksik parametre.";
            return RedirectToAction(nameof(Index), new { tenantId, entityName });
        }

        var clrType = ResolveEntityType(entityName);
        if (clrType is null)
        {
            TempData["ErrorMessage"] = $"Bilinmeyen entity tipi: {entityName}";
            return RedirectToAction(nameof(Index), new { tenantId, entityName });
        }

        var tenant = await provisioning.GetByIdAsync(tenantId, HttpContext.RequestAborted);
        if (tenant is null)
        {
            TempData["ErrorMessage"] = "Tenant bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        await using var ctx = tenantFactory.Create(tenant.ConnectionString);
        var ok = await RestoreEntityAsync(ctx, clrType, entityId, HttpContext.RequestAborted);

        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? "Kayit geri yuklendi." : "Kayit bulunamadi veya zaten aktif.";
        return RedirectToAction(nameof(Index), new { tenantId, entityName });
    }

    private Type? ResolveEntityType(string name) =>
        registry.GetSoftDeletableEntityTypes().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    private static async Task<(IReadOnlyList<DeletedEntityRow> Items, int TotalCount)> QueryDeletedAsync(
        TenantDbContext ctx, Type entityType, int skip, int take, CancellationToken ct)
    {
        var generic = typeof(SoftDeleteController)
            .GetMethod(nameof(QueryDeletedTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);
        var task = (Task)generic.Invoke(null, [ctx, skip, take, ct])!;
        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty("Result")!;
        return ((IReadOnlyList<DeletedEntityRow>, int))resultProp.GetValue(task)!;
    }

    private static async Task<(IReadOnlyList<DeletedEntityRow> Items, int TotalCount)> QueryDeletedTypedAsync<T>(
        TenantDbContext ctx, int skip, int take, CancellationToken ct)
        where T : class, ISoftDeletable
    {
        var q = ctx.Set<T>().IgnoreQueryFilters().Where(e => e.IsDeleted);
        var totalCount = await q.CountAsync(ct).ConfigureAwait(false);
        var items = await q.OrderByDescending(e => e.DeletedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        var pkProperty = ctx.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties[0].PropertyInfo
            ?? throw new InvalidOperationException($"Entity {typeof(T).Name} primary key bulunamadi.");

        IReadOnlyList<DeletedEntityRow> rows = items.Select(entity =>
        {
            var idValue = pkProperty.GetValue(entity);
            var idStr = Convert.ToString(idValue, CultureInfo.InvariantCulture) ?? string.Empty;
            return new DeletedEntityRow(idStr, GetDisplayString(entity), entity.DeletedAt);
        }).ToList();

        return (rows, totalCount);
    }

    private static async Task<bool> RestoreEntityAsync(TenantDbContext ctx, Type entityType, string entityIdRaw, CancellationToken ct)
    {
        var pkClrType = ctx.Model.FindEntityType(entityType)?.FindPrimaryKey()?.Properties[0].ClrType
            ?? throw new InvalidOperationException($"Entity {entityType.Name} primary key bulunamadi.");

        var pkValue = ConvertPk(pkClrType, entityIdRaw);
        if (pkValue is null)
        {
            return false;
        }

        var generic = typeof(SoftDeleteController)
            .GetMethod(nameof(RestoreTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);
        var task = (Task<bool>)generic.Invoke(null, [ctx, pkValue, ct])!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<bool> RestoreTypedAsync<T>(TenantDbContext ctx, object pkValue, CancellationToken ct)
        where T : class, ISoftDeletable
    {
        var pkProperty = ctx.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties[0];
        var pkParam = Expression.Parameter(typeof(T), "e");
        var pkAccess = Expression.Property(pkParam, pkProperty.PropertyInfo!);
        var pkConst = Expression.Constant(pkValue, pkProperty.ClrType);
        var predicate = Expression.Lambda<Func<T, bool>>(Expression.Equal(pkAccess, pkConst), pkParam);

        var entity = await ctx.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(predicate, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsDeleted)
        {
            return false;
        }

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    private static object? ConvertPk(Type clrType, string raw)
    {
        if (clrType == typeof(Guid))
        {
            return Guid.TryParse(raw, out var g) ? g : null;
        }
        if (clrType == typeof(int))
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
        }
        if (clrType == typeof(long))
        {
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;
        }
        try
        {
            return Convert.ChangeType(raw, clrType, CultureInfo.InvariantCulture);
        }
        catch (FormatException) { return null; }
        catch (InvalidCastException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static string GetDisplayString(object entity)
    {
        var type = entity.GetType();
        foreach (var candidate in new[] { "Title", "Name", "Slug", "DisplayName" })
        {
            var prop = type.GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null && prop.PropertyType == typeof(string))
            {
                if (prop.GetValue(entity) is string s && !string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }
        return type.Name;
    }
}
