namespace Cms.Core.Modules;

using Cms.Abstractions.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class ModuleHostExtensions
{
    public static IServiceCollection AddCmsModuleSystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ModuleLoaderOptions>(configuration.GetSection(ModuleLoaderOptions.SectionName));
        services.AddSingleton<IModuleLoader, ModuleLoader>();
        services.AddSingleton<ModuleDescriptorRegistry>();
        services.AddSingleton<IReadOnlyList<ModuleDescriptor>>(sp =>
            sp.GetRequiredService<ModuleDescriptorRegistry>().Modules);
        return services;
    }

    public static Task<IReadOnlyList<ModuleDescriptor>> LoadCmsModulesAsync(this IHost host)
    {
        var loader = host.Services.GetRequiredService<IModuleLoader>();
        var registry = host.Services.GetRequiredService<ModuleDescriptorRegistry>();

        var modules = loader.LoadAll();
        registry.Set(modules);

        return Task.FromResult(modules);
    }

    public static void MapCmsModules(this IEndpointRouteBuilder endpoints, IReadOnlyList<ModuleDescriptor> modules)
    {
        foreach (var module in modules)
        {
            if (module.Instance is IHasEndpoints hasEndpoints)
            {
                hasEndpoints.MapEndpoints(endpoints);
            }
        }
    }

    public static async Task InstallCmsModulesAsync(
        this IReadOnlyList<ModuleDescriptor> modules,
        CancellationToken cancellationToken = default)
    {
        foreach (var module in modules)
        {
            await module.Instance.OnInstallAsync(cancellationToken);
        }
    }
}
