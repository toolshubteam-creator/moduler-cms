namespace Cms.Core.Data;

using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Yalniz `dotnet ef` komutlari icin (design-time). Runtime'da TenantDbContextProvider
/// ITenantContext'ten conn string alip TenantDbContext olusturur. Migration uretirken
/// gercek tenant baglantisi olmadigi icin bu factory hardcoded MySQL 8.0 + sahte conn string ile
/// sema modelini cikartir; calistirilamaz, sadece scaffold uretir.
/// </summary>
internal sealed class TenantDbContextDesignFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseMySql(
                "Server=design-time-only;Database=tenant_design;Uid=root;Pwd=x;",
                new MySqlServerVersion(new Version(8, 0, 36)),
                mysql => mysql.MigrationsAssembly(typeof(TenantDbContext).Assembly.GetName().Name))
            .Options;

        return new TenantDbContext(options, new List<ModuleDescriptor>());
    }
}
