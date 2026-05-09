namespace Cms.Core.Modules;

using Cms.Abstractions.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ModuleHostExtensions
{
    /// <summary>ModuleLoader'i DI'ye kaydet ve modulleri yukle. RegisterServices'i her modul icin cagir.</summary>
    public static IReadOnlyList<ModuleDescriptor> AddCmsModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ModuleLoaderOptions>(configuration.GetSection(ModuleLoaderOptions.SectionName));
        services.AddSingleton<IModuleLoader, ModuleLoader>();

        var sp = services.BuildServiceProvider();
        var loader = sp.GetRequiredService<IModuleLoader>();
        var modules = loader.LoadAll();

        foreach (var module in modules)
        {
            module.Instance.RegisterServices(services, configuration);
        }

        services.AddSingleton<IReadOnlyList<ModuleDescriptor>>(modules);
        return modules;
    }

    /// <summary>Modullerin endpoint'lerini routing'e bagla.</summary>
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

    /// <summary>Tum modullerin OnInstallAsync'ini sirayla calistir.</summary>
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
