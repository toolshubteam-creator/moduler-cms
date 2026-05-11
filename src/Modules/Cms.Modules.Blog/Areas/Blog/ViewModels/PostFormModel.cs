namespace Cms.Modules.Blog.Areas.Blog.ViewModels;

using System.ComponentModel.DataAnnotations;
using Cms.Modules.Blog.Contracts;

public sealed class PostFormModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Bos birakilirsa Title'dan otomatik uretilir.</summary>
    [StringLength(200)]
    public string? Slug { get; set; }

    [StringLength(500)]
    public string? Excerpt { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public PostStatus Status { get; set; } = PostStatus.Draft;

    public DateTime? PublishAt { get; set; }

    public int? FeaturedMediaId { get; set; }

    /// <summary>Multi-select &lt;select multiple&gt;'dan gelir; bos olabilir.</summary>
    public List<int> CategoryIds { get; set; } = [];

    /// <summary>Comma-separated free-text tag input. Bos olabilir.</summary>
    [StringLength(1000)]
    public string? TagsInput { get; set; }

    // Inline SEO (Faz-4.3 SeoMetaInput alanlariyla birebir hizali — Keywords / CanonicalUrl YOK)
    [StringLength(200)] public string? SeoTitle { get; set; }
    [StringLength(500)] public string? SeoDescription { get; set; }
    [StringLength(500)] public string? SeoOgImage { get; set; }
    [StringLength(500)] public string? SeoCanonical { get; set; }
    [StringLength(100)] public string? SeoRobots { get; set; }
}
