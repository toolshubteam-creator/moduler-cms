namespace Cms.Core.Data;

using Cms.Core.Auth;
using Cms.Core.Authorization;
using Cms.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
        return services;
    }

    public static IServiceCollection AddCmsTenantData(this IServiceCollection services)
    {
        services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped<TenantDbContextProvider>();
        services.AddScoped<TenantDbContext>(sp => sp.GetRequiredService<TenantDbContextProvider>().Get());
        return services;
    }

    public static IServiceCollection AddCmsAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, HasPermissionPolicyProvider>();
        services.AddScoped<PermissionSeeder>();
        return services;
    }
}
