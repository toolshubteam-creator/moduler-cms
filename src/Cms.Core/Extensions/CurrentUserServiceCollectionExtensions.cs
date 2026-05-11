namespace Cms.Core.Extensions;

using Microsoft.Extensions.DependencyInjection;

public static class CurrentUserServiceCollectionExtensions
{
    /// <summary>
    /// ICurrentUserService gerektiren altyapi (audit interceptor) icin marker.
    /// Implementasyon (HttpCurrentUserService veya alternatif) Cms.Web tarafinda
    /// Program.cs'te <c>AddScoped&lt;ICurrentUserService, ...&gt;()</c> ile kayit edilir.
    /// IHttpContextAccessor zaten host tarafindan saglanir.
    /// </summary>
    public static IServiceCollection AddCmsCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        return services;
    }
}
