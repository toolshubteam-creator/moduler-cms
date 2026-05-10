namespace Cms.Core.Tenancy;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class SubdomainTenantResolver(
    MasterDbContext db,
    IOptions<TenancyOptions> options) : ITenantResolver
{
    private readonly TenancyOptions _options = options.Value;

    public async Task<Tenant?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var slug = ExtractSlug(context);
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }

        return await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, cancellationToken);
    }

    private string? ExtractSlug(HttpContext context)
    {
        var host = context.Request.Host.Host;

        if (!string.IsNullOrEmpty(_options.RootDomain) &&
            host.EndsWith($".{_options.RootDomain}", StringComparison.OrdinalIgnoreCase))
        {
            var slug = host[..^(_options.RootDomain.Length + 1)];
            if (!string.IsNullOrEmpty(slug) && !slug.Contains('.', StringComparison.Ordinal))
            {
                return slug.ToLowerInvariant();
            }
        }

        if (_options.AllowQueryFallback && context.Request.Query.TryGetValue("tenant", out var queryTenant))
        {
            var fallback = queryTenant.ToString();
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback.Trim().ToLowerInvariant();
            }
        }

        return null;
    }
}
