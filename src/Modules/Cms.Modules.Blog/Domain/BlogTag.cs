namespace Cms.Modules.Blog.Domain;

using Cms.Core.Domain.Auditing;

public sealed class BlogTag : IAuditable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
