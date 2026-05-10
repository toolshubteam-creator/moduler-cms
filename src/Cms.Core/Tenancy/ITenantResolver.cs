namespace Cms.Core.Tenancy;

using Cms.Core.Data.Entities;
using Microsoft.AspNetCore.Http;

public interface ITenantResolver
{
    Task<Tenant?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
