namespace Cms.Core.Data;

using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Yalniz `dotnet ef` komutlari icin (design-time). Runtime'da TenantDbContextProvider
/// ITenantContext'ten conn string alip TenantDbContext olusturur. Migration uretirken
/// gercek tenant baglantisi olmadigi icin bu factory hardcoded MySQL 8.0 + sahte conn string ile
/// sema modelini cikartir; calistirilamaz, sadece scaffold uretir.
///
/// Faz-4.1: Modul entity'leri (orn. Settings_Entries) migration'a dahil olmasi icin design-time'da
/// Cms.Web'in bin/.../Modules/ klasorunden modul DLL'lerini kesfedip ModuleDescriptor list'ine
/// koyariz. Startup-project Cms.Web oldugu icin AppContext.BaseDirectory Cms.Web'in cikti
/// klasorudur; Modules alt klasoru build-time'da src/Modules/Directory.Build.targets ile
/// populate edilir.
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

        var modulesPath = Path.Combine(AppContext.BaseDirectory, "Modules");
        var moduleOptions = new ModuleLoaderOptions { Path = modulesPath };
        var loader = new ModuleLoader(Options.Create(moduleOptions), NullLogger<ModuleLoader>.Instance);
        var modules = loader.LoadAll();

        return new TenantDbContext(options, modules);
    }
}
