namespace Cms.Modules.Media;

using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Modules.Media.Contracts;
using Cms.Modules.Media.Domain;
using Cms.Modules.Media.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

public sealed class MediaModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "media",
        Name = "Media",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "Tenant-scoped media library with hash-based dedup",
        Author = "Cms.Core",
        IsCorePlugin = true,
    };

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));
        services.AddScoped<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IMediaService, MediaService>();
    }

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaFileEntity>(b =>
        {
            b.ToTable("Media_Files");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedOnAdd();
            b.Property(e => e.FileName).HasMaxLength(500).IsRequired();
            b.Property(e => e.StoredPath).HasMaxLength(500).IsRequired();
            b.Property(e => e.MimeType).HasMaxLength(200).IsRequired();
            b.Property(e => e.SizeBytes).IsRequired();
            b.Property(e => e.Hash).HasMaxLength(64).IsRequired();
            b.Property(e => e.AltText).HasMaxLength(500);
            b.Property(e => e.UploadedAt).IsRequired();
            b.Property(e => e.IsDeleted).IsRequired();
            b.HasIndex(e => e.Hash);
            b.HasIndex(e => e.UploadedAt);
        });
    }

    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "media.files.view", DisplayName = "Media Dosyalari Goruntule", Description = "Yuklenmis dosyalari listele/onizle" },
        new() { Key = "media.files.upload", DisplayName = "Media Dosyasi Yukle", Description = "Yeni dosya yukle ve metadata duzenle" },
        new() { Key = "media.files.delete", DisplayName = "Media Dosyasi Sil", Description = "Soft-delete (geri yuklenebilir)" },
    ];
}
