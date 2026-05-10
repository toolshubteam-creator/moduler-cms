namespace Cms.Web.Models.Admin;

using System.ComponentModel.DataAnnotations;

public sealed class CreateTenantViewModel
{
    [Required(ErrorMessage = "Slug zorunlu.")]
    [StringLength(31, MinimumLength = 3, ErrorMessage = "Slug 3-31 karakter olmali.")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9-]*$", ErrorMessage = "Slug harf ile baslamali, sadece harf-rakam-tire icermeli.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Goruntu adi zorunlu.")]
    [StringLength(256, MinimumLength = 1)]
    [Display(Name = "Goruntu Adi")]
    public string DisplayName { get; set; } = string.Empty;
}
