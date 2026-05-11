namespace Cms.Core.Data;

using Cms.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed record TenantMigrationReport(int Successful, int Failed, int Total);

/// <summary>
/// Tum aktif tenant'larin tenant DB'lerine pending migration'lari uygular. Sequential
/// calisir — paralel calistirma kasitli olarak DENENMEDI (DB lock contention + log
/// karmasi). Tek tenant fail olursa exception swallow + log + sonraki tenant'a devam.
/// EF Core MigrateAsync zaten idempotent; tekrar cagrildiginda no-op.
/// </summary>
public sealed class TenantMigrationRunner(
    ILogger<TenantMigrationRunner> logger,
    ITenantProvisioningService provisioning,
    ITenantDbContextFactory tenantFactory)
{
    public async Task<TenantMigrationReport> MigrateAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await provisioning.ListAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);
        var successful = 0;
        var failed = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var ctx = tenantFactory.Create(tenant.ConnectionString);
                await ctx.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                successful++;
                logger.LogInformation("Tenant '{Slug}' migrated", tenant.Slug);
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Tenant '{Slug}' migration patladi", tenant.Slug);
            }
        }

        return new TenantMigrationReport(successful, failed, tenants.Count);
    }
}
