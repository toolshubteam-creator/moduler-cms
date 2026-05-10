namespace Cms.Core.Tenancy;

using Cms.Core.Data.Entities;

public interface ITenantProvisioningService
{
    Task<TenantProvisioningResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
