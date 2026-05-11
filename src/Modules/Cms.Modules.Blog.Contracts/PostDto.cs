namespace Cms.Modules.Blog.Contracts;

public sealed record PostDto(
    int Id,
    string Title,
    string Slug,
    string? Excerpt,
    string Content,
    PostStatus Status,
    DateTime? PublishAt,
    DateTime? PublishedAt,
    int? FeaturedMediaId,
    int AuthorUserId,
    IReadOnlyList<int> CategoryIds,
    IReadOnlyList<string> TagNames);
