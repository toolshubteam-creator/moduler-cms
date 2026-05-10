namespace Cms.Core.Tenancy;

using Cms.Core.Data.Entities;

public interface ITenantContext
{
    Tenant? Current { get; }
    bool IsResolved { get; }
    void Set(Tenant tenant);
}
