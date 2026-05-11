namespace Cms.Modules.Seo.TagHelpers;

using System.Text;
using System.Text.Encodings.Web;
using Cms.Modules.Seo.Contracts;
using Microsoft.AspNetCore.Razor.TagHelpers;

/// <summary>
/// Razor view'larda <c>&lt;seo-meta target-type="post" target-id="42" /&gt;</c> ile cagrilan
/// inline tag. ISeoMetaService.ResolveAsync ile meta + Settings default'lari karistirip
/// title/description/og:image/canonical/robots HTML tag'lerini render eder. Tum content
/// HtmlEncoder ile escape edilir.
/// </summary>
[HtmlTargetElement("seo-meta", TagStructure = TagStructure.WithoutEndTag)]
public sealed class SeoMetaTagHelper(ISeoMetaService seo) : TagHelper
{
    [HtmlAttributeName("target-type")]
    public string TargetType { get; set; } = string.Empty;

    [HtmlAttributeName("target-id")]
    public string TargetId { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // Wrapper tag yazma — sadece icerik
        output.TagName = null;

        if (string.IsNullOrWhiteSpace(TargetType) || string.IsNullOrWhiteSpace(TargetId))
        {
            output.Content.SetHtmlContent(string.Empty);
            return;
        }

        var resolved = await seo.ResolveAsync(TargetType, TargetId).ConfigureAwait(false);
        var sb = new StringBuilder();
        var encoder = HtmlEncoder.Default;

        if (!string.IsNullOrWhiteSpace(resolved.Title))
        {
            sb.AppendLine($"<title>{encoder.Encode(resolved.Title)}</title>");
        }
        if (!string.IsNullOrWhiteSpace(resolved.Description))
        {
            sb.AppendLine($"<meta name=\"description\" content=\"{encoder.Encode(resolved.Description)}\" />");
        }
        if (!string.IsNullOrWhiteSpace(resolved.OgImage))
        {
            sb.AppendLine($"<meta property=\"og:image\" content=\"{encoder.Encode(resolved.OgImage)}\" />");
        }
        if (!string.IsNullOrWhiteSpace(resolved.Canonical))
        {
            sb.AppendLine($"<link rel=\"canonical\" href=\"{encoder.Encode(resolved.Canonical)}\" />");
        }
        if (!string.IsNullOrWhiteSpace(resolved.Robots))
        {
            sb.AppendLine($"<meta name=\"robots\" content=\"{encoder.Encode(resolved.Robots)}\" />");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}
