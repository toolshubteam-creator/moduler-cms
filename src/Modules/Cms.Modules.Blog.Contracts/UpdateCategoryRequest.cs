namespace Cms.Modules.Blog.Contracts;

public sealed record UpdateCategoryRequest(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int? ParentCategoryId);
