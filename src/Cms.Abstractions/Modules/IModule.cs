namespace Cms.Abstractions.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tum modullerin implement etmesi gereken cekirdek kontrat.
/// Opsiyonel yetenekler icin IHasEntities, IHasEndpoints, IHasPermissions, IHasMenuItems
/// arayuzlerini ek olarak implement edin (veya ModuleBase'den miras alin).
/// </summary>
public interface IModule
{
    ModuleManifest Manifest { get; }

    /// <summary>DI servislerini kaydet.</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Modul yuklendiginde calisir (DB seed, ilk kurulum).</summary>
    Task OnInstallAsync(CancellationToken cancellationToken = default);

    /// <summary>Modul kaldirildiginda calisir (cleanup).</summary>
    Task OnUninstallAsync(CancellationToken cancellationToken = default);
}
