namespace Cms.Core.Data;

using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;

public interface ITenantDbContextFactory
{
    TenantDbContext Create(string connectionString);
}

public sealed class TenantDbContextFactory(IReadOnlyList<ModuleDescriptor> modules) : ITenantDbContextFactory
{
    public TenantDbContext Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(TenantDbContext).Assembly.GetName().Name))
            .Options;

        return new TenantDbContext(options, modules);
    }
}
