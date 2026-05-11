namespace Cms.Core.Data;

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

    public TenantDbContextFactory(IReadOnlyList<ModuleDescriptor> modules)
        : this(modules, [])
    {
    }

    public TenantDbContextFactory(IReadOnlyList<ModuleDescriptor> modules, IEnumerable<IInterceptor> interceptors)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(interceptors);
        _modules = modules;
        _interceptors = [.. interceptors];
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

        return new TenantDbContext(builder.Options, _modules);
    }
}
