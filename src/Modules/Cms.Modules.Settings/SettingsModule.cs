namespace Cms.Modules.Settings;

using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Modules.Settings.Contracts;
using Cms.Modules.Settings.Domain;
using Cms.Modules.Settings.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

public sealed class SettingsModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "settings",
        Name = "Settings",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "Tenant-scoped key/value settings (Faz-4.1)",
        Author = "Cms.Core",
        IsCorePlugin = true,
    };

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISettingsService, SettingsService>();
    }

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingEntryEntity>(b =>
        {
            b.ToTable("Settings_Entries");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedOnAdd();
            b.Property(e => e.Key).HasMaxLength(200).IsRequired();
            b.HasIndex(e => e.Key).IsUnique();
            b.Property(e => e.Value).HasColumnType("text").IsRequired();
            b.Property(e => e.ValueType).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(e => e.UpdatedAt).IsRequired();
        });
    }

    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "settings.view", DisplayName = "Settings Goruntule", Description = "Tenant ayarlarini goruntule" },
        new() { Key = "settings.edit", DisplayName = "Settings Duzenle", Description = "Tenant ayarlarini olustur/duzenle/sil" },
    ];
}
