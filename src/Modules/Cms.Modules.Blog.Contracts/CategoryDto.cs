namespace Cms.Modules.Blog.Contracts;

public sealed record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int? ParentCategoryId);

/// <summary>Indent (UI agacli gosterim) icin Depth bilgisi katilmis varyant.</summary>
public sealed record CategoryTreeNodeDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int? ParentCategoryId,
    int Depth);
