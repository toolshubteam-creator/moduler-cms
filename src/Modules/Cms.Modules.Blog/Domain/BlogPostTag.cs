namespace Cms.Modules.Blog.Domain;

/// <summary>Composite PK (PostId, TagId). Tag tarafi FK CASCADE.</summary>
public sealed class BlogPostTag
{
    public int PostId { get; set; }

    public int TagId { get; set; }
}
