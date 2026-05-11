namespace Cms.Modules.Blog.Areas.Blog.ViewModels;

using System.ComponentModel.DataAnnotations;

public sealed class TagFormModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Slug { get; set; }
}
