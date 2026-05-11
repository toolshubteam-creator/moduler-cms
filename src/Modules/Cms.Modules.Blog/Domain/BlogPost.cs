namespace Cms.Modules.Blog.Domain;

using Cms.Core.Domain.Auditing;
using Cms.Modules.Blog.Contracts;

public sealed class BlogPost : IAuditable, ISoftDeletable
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Excerpt { get; set; }

    public string Content { get; set; } = string.Empty;

    public PostStatus Status { get; set; }

    /// <summary>Schema'da var ama yorum YOK (Faz-6 Hangfire ile interpret edilecek).</summary>
    public DateTime? PublishAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>Soft FK to Media_Files.Id (Kural 5 — modul-arasi navigation property yok).</summary>
    public int? FeaturedMediaId { get; set; }

    /// <summary>Soft FK to Sys_Users.Id (Master DB — tenant DB'den join yok).</summary>
    public int AuthorUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
