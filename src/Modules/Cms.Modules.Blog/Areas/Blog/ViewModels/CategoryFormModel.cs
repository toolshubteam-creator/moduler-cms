namespace Cms.Modules.Blog.Areas.Blog.ViewModels;

using System.ComponentModel.DataAnnotations;

public sealed class CategoryFormModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Create'te bos birakilabilir (Name'den uretilir). Edit'te zorunlu.</summary>
    [StringLength(200)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public int? ParentCategoryId { get; set; }
}
