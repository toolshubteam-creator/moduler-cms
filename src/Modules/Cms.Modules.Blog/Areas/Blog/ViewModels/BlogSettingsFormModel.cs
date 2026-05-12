namespace Cms.Modules.Blog.Areas.Blog.ViewModels;

using System.ComponentModel.DataAnnotations;

public sealed class BlogSettingsFormModel
{
    [Required]
    [StringLength(200)]
    public string UrlPattern { get; set; } = "/blog/{slug}";

    [Range(1, 100, ErrorMessage = "1 ile 100 arasinda olmalidir.")]
    public int PostsPerPage { get; set; } = 10;

    [StringLength(200)]
    public string? DefaultMetaTitle { get; set; }

    [StringLength(500)]
    public string? DefaultMetaDescription { get; set; }

    public bool ShowExcerptInList { get; set; } = true;
}
