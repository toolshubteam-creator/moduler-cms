namespace Cms.Modules.Seo;

using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Modules.Seo.Contracts;
using Cms.Modules.Seo.Domain;
using Cms.Modules.Seo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

public sealed class SeoModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "seo",
        Name = "SEO",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "Per-target SEO meta tags + render TagHelper",
        Author = "Cms.Core",
        IsCorePlugin = true,
    };

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISeoMetaService, SeoMetaService>();
    }

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeoMetaEntity>(b =>
        {
            b.ToTable("Seo_Metas");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedOnAdd();
            b.Property(e => e.TargetType).HasMaxLength(100).IsRequired();
            b.Property(e => e.TargetId).HasMaxLength(200).IsRequired();
            b.Property(e => e.Title).HasMaxLength(200);
            b.Property(e => e.Description).HasMaxLength(500);
            b.Property(e => e.OgImage).HasMaxLength(500);
            b.Property(e => e.Canonical).HasMaxLength(500);
            b.Property(e => e.Robots).HasMaxLength(100);
            b.Property(e => e.UpdatedAt).IsRequired();
            b.HasIndex(e => new { e.TargetType, e.TargetId }).IsUnique();
        });
    }

    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "seo.metas.view", DisplayName = "SEO Meta Goruntule", Description = "Tenant SEO metalarini listele ve oku" },
        new() { Key = "seo.metas.edit", DisplayName = "SEO Meta Duzenle", Description = "SEO meta olustur, guncelle, sil" },
    ];
}
