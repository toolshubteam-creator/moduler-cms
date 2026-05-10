namespace Cms.Core.Tenancy;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    private readonly TenancyOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsBypassed(path))
        {
            await next(context);
            return;
        }

        var tenant = await resolver.ResolveAsync(context, context.RequestAborted);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Tenant bulunamadi.");
            return;
        }

        tenantContext.Set(tenant);
        await next(context);
    }

    private bool IsBypassed(string path)
    {
        foreach (var prefix in _options.BypassPaths)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
