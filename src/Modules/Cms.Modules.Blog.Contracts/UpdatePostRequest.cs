namespace Cms.Modules.Blog.Contracts;

public sealed record UpdatePostRequest(
    int Id,
    string Title,
    string Slug,
    string? Excerpt,
    string Content,
    PostStatus Status,
    DateTime? PublishAt,
    int? FeaturedMediaId,
    IReadOnlyList<int> CategoryIds,
    IReadOnlyList<string> TagNames);
