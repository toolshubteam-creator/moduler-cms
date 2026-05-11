namespace Cms.Core.Data.Interceptors;

using Cms.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// ISoftDeletable entity'leri icin EntityState.Deleted'i EntityState.Modified'a cevirir,
/// IsDeleted=true ve DeletedAt=UtcNow yazar. AuditSaveChangesInterceptor'dan ONCE
/// kayit edilir (TenantDataServiceCollectionExtensions). Audit interceptor sonra calisip
/// IsDeleted false->true gecisini gorerek AuditAction.Delete olarak siniflandirir.
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Apply(DbContext? ctx)
    {
        if (ctx is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in ctx.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
            }
        }
    }
}
