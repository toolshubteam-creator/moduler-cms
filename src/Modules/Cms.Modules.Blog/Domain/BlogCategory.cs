namespace Cms.Modules.Blog.Domain;

using Cms.Core.Domain.Auditing;

public sealed class BlogCategory : IAuditable, ISoftDeletable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Self-FK; null = root kategori.</summary>
    public int? ParentCategoryId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
