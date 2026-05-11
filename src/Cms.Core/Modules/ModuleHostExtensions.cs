namespace Cms.Core.Modules;

using Cms.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public static class ModuleHostExtensions
{
    /// <summary>
    /// Modul altyapi servislerini DI'ye kayit eder (IModuleLoader, ModuleLoaderOptions,
    /// ModuleDescriptorRegistry placeholder). Modul DLL'lerini YUKLEMEZ — yukleme
    /// <see cref="UseCmsModules"/> ile build oncesi yapilir.
    /// </summary>
    public static IServiceCollection AddCmsModuleSystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ModuleLoaderOptions>(configuration.GetSection(ModuleLoaderOptions.SectionName));
        services.AddSingleton<IModuleLoader, ModuleLoader>();
        // UseCmsModules cagrilmazsa registry bos kalir — backwards compat / test senaryolari icin.
        services.AddSingleton<ModuleDescriptorRegistry>();
        services.AddSingleton<IReadOnlyList<ModuleDescriptor>>(sp =>
            sp.GetRequiredService<ModuleDescriptorRegistry>().Modules);
        return services;
    }

    /// <summary>
    /// Modul DLL'lerini Cms.Web'in <c>Modules/</c> klasorunden kesfedip yukler ve her modulun
    /// <see cref="IModule.RegisterServices"/> metodunu DI build oncesi cagirir (D-014 cozumu).
    /// Modul assembly'leri MVC <see cref="Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPart"/>
    /// olarak eklenir; controller/view discovery devreye girer.
    /// </summary>
    public static WebApplicationBuilder UseCmsModules(this WebApplicationBuilder builder)
    {
        builder.Services.AddCmsModuleSystem(builder.Configuration);

        var loaderOptions = new ModuleLoaderOptions();
        builder.Configuration.GetSection(ModuleLoaderOptions.SectionName).Bind(loaderOptions);

        // Modul Contracts DLL'lerini default ALC'ye eagerly yukle. Modul aralarinda
        // (orn. Cms.Modules.Seo → Cms.Modules.Settings.Contracts kullanimi) tip identity'nin
        // paylasilmasi icin sart. Aksi halde her modul ALC'si kendi Contracts kopyasini
        // yukler ve DI graph cross-module dependency'leri cozemez (Faz-4.3 calibration).
        if (Directory.Exists(loaderOptions.Path))
        {
            foreach (var contractsDll in Directory.GetFiles(loaderOptions.Path, "Cms.Modules.*.Contracts.dll"))
            {
                try
                {
                    System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(contractsDll);
                }
                catch (BadImageFormatException) { }
                catch (FileLoadException) { }
            }
        }

        var loader = new ModuleLoader(Options.Create(loaderOptions), NullLogger<ModuleLoader>.Instance);
        var modules = loader.LoadAll();

        foreach (var m in modules)
        {
            m.Instance.RegisterServices(builder.Services, builder.Configuration);
        }

        var mvcBuilder = builder.Services.AddControllersWithViews();
        foreach (var m in modules)
        {
            mvcBuilder.AddApplicationPart(m.Assembly);
        }

        // Pre-populated registry singleton override — AddCmsModuleSystem'in placeholder'ini kapatir.
        var registry = new ModuleDescriptorRegistry();
        registry.Set(modules);
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton<IReadOnlyList<ModuleDescriptor>>(modules);

        return builder;
    }

    /// <summary>
    /// Legacy: build-sonrasi yukleme. <see cref="UseCmsModules"/> kullanilmazsa
    /// fallback olarak modulleri discover edip registry'ye yazar. UseCmsModules ile
    /// birlikte cagrildiginda registry zaten dolu — tekrar overwrite olmasin diye
    /// bu metodu artik Program.cs cagirmiyor; tutuluyor ki backwards-compat testler kirilmasin.
    /// </summary>
    public static Task<IReadOnlyList<ModuleDescriptor>> LoadCmsModulesAsync(this IHost host)
    {
        var loader = host.Services.GetRequiredService<IModuleLoader>();
        var registry = host.Services.GetRequiredService<ModuleDescriptorRegistry>();

        if (registry.Modules.Count > 0)
        {
            return Task.FromResult(registry.Modules);
        }

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
