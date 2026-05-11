namespace Cms.Modules.Blog;

using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Cms.Modules.Blog.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

public sealed class BlogModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "blog",
        Name = "Blog",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "Reference blog module — Post entity + Media/SEO integration",
        Author = "Cms.Core",
        IsCorePlugin = false,
        Dependencies =
        [
            new ModuleDependency { ModuleId = "media", VersionRange = VersionRange.Parse("[1.0.0,2.0.0)") },
            new ModuleDependency { ModuleId = "seo",   VersionRange = VersionRange.Parse("[1.0.0,2.0.0)") },
        ],
    };

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPostService, PostService>();
    }

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlogPost>(b =>
        {
            b.ToTable("Blog_Posts");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedOnAdd();
            b.Property(p => p.Title).HasMaxLength(200).IsRequired();
            b.Property(p => p.Slug).HasMaxLength(200).IsRequired();
            b.Property(p => p.Excerpt).HasMaxLength(500);
            b.Property(p => p.Content).HasColumnType("longtext").IsRequired();
            b.Property(p => p.Status).HasConversion<int>().IsRequired();
            b.Property(p => p.PublishAt);
            b.Property(p => p.PublishedAt);
            b.Property(p => p.FeaturedMediaId);
            b.Property(p => p.AuthorUserId).IsRequired();
            b.Property(p => p.IsDeleted).IsRequired();
            b.Property(p => p.DeletedAt);
            b.HasIndex(p => p.Slug).IsUnique();
            b.HasIndex(p => p.Status);
            b.HasIndex(p => p.PublishedAt);
        });
    }

    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "blog.posts.view",    DisplayName = "Blog Yazilarini Goruntule", Description = "Blog post listesi ve detay" },
        new() { Key = "blog.posts.create",  DisplayName = "Blog Yazisi Olustur",       Description = "Yeni post yarat" },
        new() { Key = "blog.posts.edit",    DisplayName = "Blog Yazisi Duzenle",        Description = "Mevcut post'u duzenle" },
        new() { Key = "blog.posts.delete",  DisplayName = "Blog Yazisi Sil",            Description = "Soft-delete (geri yuklenebilir)" },
        new() { Key = "blog.posts.publish", DisplayName = "Blog Yazisi Yayinla",         Description = "Draft <-> Published gecisi" },
    ];
}
