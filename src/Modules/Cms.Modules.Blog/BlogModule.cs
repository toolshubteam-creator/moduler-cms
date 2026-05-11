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
        Description = "Reference blog module — Post entity + Media/SEO integration + Category/Tag",
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
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
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

        modelBuilder.Entity<BlogCategory>(b =>
        {
            b.ToTable("Blog_Categories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Id).ValueGeneratedOnAdd();
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Slug).HasMaxLength(200).IsRequired();
            b.Property(c => c.Description).HasMaxLength(1000);
            b.Property(c => c.ParentCategoryId);
            b.Property(c => c.IsDeleted).IsRequired();
            b.Property(c => c.DeletedAt);
            b.HasIndex(c => c.Slug).IsUnique();
            b.HasIndex(c => c.ParentCategoryId);
            b.HasOne<BlogCategory>()
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BlogTag>(b =>
        {
            b.ToTable("Blog_Tags");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).ValueGeneratedOnAdd();
            b.Property(t => t.Name).HasMaxLength(100).IsRequired();
            b.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<BlogPostCategory>(b =>
        {
            b.ToTable("Blog_PostCategories");
            b.HasKey(pc => new { pc.PostId, pc.CategoryId });
            b.HasOne<BlogPost>()
                .WithMany()
                .HasForeignKey(pc => pc.PostId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BlogCategory>()
                .WithMany()
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(pc => pc.CategoryId);
        });

        modelBuilder.Entity<BlogPostTag>(b =>
        {
            b.ToTable("Blog_PostTags");
            b.HasKey(pt => new { pt.PostId, pt.TagId });
            b.HasOne<BlogPost>()
                .WithMany()
                .HasForeignKey(pt => pt.PostId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BlogTag>()
                .WithMany()
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(pt => pt.TagId);
        });
    }

    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "blog.posts.view",        DisplayName = "Blog Yazilarini Goruntule", Description = "Blog post listesi ve detay" },
        new() { Key = "blog.posts.create",      DisplayName = "Blog Yazisi Olustur",        Description = "Yeni post yarat" },
        new() { Key = "blog.posts.edit",        DisplayName = "Blog Yazisi Duzenle",         Description = "Mevcut post'u duzenle" },
        new() { Key = "blog.posts.delete",      DisplayName = "Blog Yazisi Sil",             Description = "Soft-delete (geri yuklenebilir)" },
        new() { Key = "blog.posts.publish",     DisplayName = "Blog Yazisi Yayinla",         Description = "Draft <-> Published gecisi" },
        new() { Key = "blog.categories.view",   DisplayName = "Blog Kategorilerini Goruntule", Description = "Kategori listesi" },
        new() { Key = "blog.categories.create", DisplayName = "Blog Kategorisi Olustur",      Description = "Yeni kategori yarat" },
        new() { Key = "blog.categories.edit",   DisplayName = "Blog Kategorisi Duzenle",      Description = "Mevcut kategoriyi duzenle" },
        new() { Key = "blog.categories.delete", DisplayName = "Blog Kategorisi Sil",          Description = "Soft-delete (alt kategorisi olmayan)" },
        new() { Key = "blog.tags.view",         DisplayName = "Blog Etiketlerini Goruntule",  Description = "Etiket listesi" },
        new() { Key = "blog.tags.create",       DisplayName = "Blog Etiketi Olustur",         Description = "Yeni etiket yarat" },
        new() { Key = "blog.tags.edit",         DisplayName = "Blog Etiketi Duzenle",         Description = "Mevcut etiketi duzenle" },
        new() { Key = "blog.tags.delete",       DisplayName = "Blog Etiketi Sil",             Description = "Hard delete (junction cascade)" },
    ];
}
