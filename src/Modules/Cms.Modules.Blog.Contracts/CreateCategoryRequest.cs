namespace Cms.Modules.Blog.Contracts;

public sealed record CreateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    int? ParentCategoryId);
