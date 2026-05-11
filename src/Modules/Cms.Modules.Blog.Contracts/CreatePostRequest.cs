namespace Cms.Modules.Blog.Contracts;

public sealed record CreatePostRequest(
    string Title,
    string? Slug,
    string? Excerpt,
    string Content,
    PostStatus Status,
    DateTime? PublishAt,
    int? FeaturedMediaId,
    int AuthorUserId,
    IReadOnlyList<int> CategoryIds,
    IReadOnlyList<string> TagNames);
