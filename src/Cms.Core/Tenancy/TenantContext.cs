namespace Cms.Core.Tenancy;

using Cms.Core.Data.Entities;

internal sealed class TenantContext : ITenantContext
{
    public Tenant? Current { get; private set; }

    public bool IsResolved => Current is not null;

    public void Set(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (Current is not null)
        {
            throw new InvalidOperationException("TenantContext zaten cozumlenmis.");
        }

        Current = tenant;
    }
}
