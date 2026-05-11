namespace Cms.Core.Data;

using Cms.Core.Auth;
using Cms.Core.Authorization;
using Cms.Core.Data.Interceptors;
using Cms.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class MasterDataExtensions
{
    public static IServiceCollection AddCmsMaster(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Master")
            ?? throw new InvalidOperationException("ConnectionStrings:Master tanimli degil.");

        services.AddDbContext<MasterDbContext>(options =>
            options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    public static IServiceCollection AddCmsTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        return services;
    }

    public static IServiceCollection AddCmsTenantData(this IServiceCollection services)
    {
        // SoftDelete ONCE Audit'ten kayit edilir; ikisi de IInterceptor olarak resolve edilir
        // ve DbContextOptionsBuilder.AddInterceptors'a registration sirasi ile gecirilir.
        services.AddSingleton<IInterceptor, SoftDeleteInterceptor>();
        services.AddSingleton<IInterceptor, AuditSaveChangesInterceptor>();

        services.AddSingleton<ITenantDbContextFactory>(sp => new TenantDbContextFactory(
            sp.GetRequiredService<IReadOnlyList<Cms.Core.Modules.ModuleDescriptor>>(),
            sp.GetServices<IInterceptor>()));
        services.AddScoped<TenantDbContextProvider>();
        services.AddScoped<TenantDbContext>(sp => sp.GetRequiredService<TenantDbContextProvider>().Get());
        return services;
    }

    public static IServiceCollection AddCmsAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();
        services.AddScoped<IAuthorizationHandler, SystemRoleHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, HasPermissionPolicyProvider>();
        services.AddScoped<PermissionSeeder>();
        return services;
    }
}
