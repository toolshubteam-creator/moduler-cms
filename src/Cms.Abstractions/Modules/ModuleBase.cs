namespace Cms.Abstractions.Modules;

using Cms.Abstractions.Modules.Menu;
using Cms.Abstractions.Modules.Permissions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Modullerin miras alabilecegi temel sinif. Opsiyonel arayuzleri default no-op
/// olarak implement eder; modul sadece ihtiyaci olanlari override eder.
/// </summary>
public abstract class ModuleBase : IModule, IHasEntities, IHasEndpoints, IHasPermissions, IHasMenuItems
{
    public abstract ModuleManifest Manifest { get; }

    public virtual void RegisterServices(IServiceCollection services, IConfiguration configuration) { }

    public virtual void RegisterEntities(ModelBuilder modelBuilder) { }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints) { }

    public virtual IReadOnlyList<PermissionDescriptor> GetPermissions() => [];

    public virtual IReadOnlyList<MenuItem> GetMenuItems() => [];

    public virtual Task OnInstallAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public virtual Task OnUninstallAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
