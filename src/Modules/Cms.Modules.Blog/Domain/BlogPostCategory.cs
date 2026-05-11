namespace Cms.Modules.Blog.Domain;

/// <summary>Composite PK (PostId, CategoryId). IAuditable DEGIL — sadece bag/koz.</summary>
public sealed class BlogPostCategory
{
    public int PostId { get; set; }

    public int CategoryId { get; set; }
}
