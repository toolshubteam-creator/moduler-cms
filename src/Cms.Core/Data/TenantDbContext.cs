namespace Cms.Core.Data;

using Cms.Abstractions.Modules;
using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;

public class TenantDbContext(
    DbContextOptions<TenantDbContext> options,
    IReadOnlyList<ModuleDescriptor> modules) : DbContext(options)
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules = modules;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var module in _modules)
        {
            if (module.Instance is IHasEntities hasEntities)
            {
                hasEntities.RegisterEntities(modelBuilder);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
