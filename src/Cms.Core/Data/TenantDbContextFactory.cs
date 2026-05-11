namespace Cms.Core.Data;

using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

public interface ITenantDbContextFactory
{
    TenantDbContext Create(string connectionString);
}

public sealed class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules;
    private readonly IInterceptor[] _interceptors;
    private readonly ModuleDescriptorRegistry? _registry;

    public TenantDbContextFactory(IReadOnlyList<ModuleDescriptor> modules)
        : this(modules, [], registry: null)
    {
    }

    public TenantDbContextFactory(IReadOnlyList<ModuleDescriptor> modules, IEnumerable<IInterceptor> interceptors)
        : this(modules, interceptors, registry: null)
    {
    }

    public TenantDbContextFactory(
        IReadOnlyList<ModuleDescriptor> modules,
        IEnumerable<IInterceptor> interceptors,
        ModuleDescriptorRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(interceptors);
        _modules = modules;
        _interceptors = [.. interceptors];
        _registry = registry;
    }

    public TenantDbContext Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new DbContextOptionsBuilder<TenantDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(TenantDbContext).Assembly.GetName().Name))
            .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>();

        if (_interceptors.Length > 0)
        {
            builder.AddInterceptors(_interceptors);
        }

        var ctx = new TenantDbContext(builder.Options, _modules, _registry);

        // Registry populate ctx.Model uzerinden — OnModelCreating cache'lendigi icin
        // bu cross-cut her Create cagrisinda registry'yi gunceller (idempotent).
        if (_registry is not null)
        {
            var softDeletable = ctx.Model.GetEntityTypes()
                .Where(et => typeof(ISoftDeletable).IsAssignableFrom(et.ClrType))
                .Select(et => et.ClrType);
            _registry.RegisterSoftDeletableTypes(softDeletable);
        }

        return ctx;
    }
}
