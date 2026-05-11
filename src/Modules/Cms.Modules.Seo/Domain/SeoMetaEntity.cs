namespace Cms.Modules.Seo.Domain;

using Cms.Core.Domain.Auditing;

public sealed class SeoMetaEntity : IAuditable
{
    public int Id { get; set; }

    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? OgImage { get; set; }

    public string? Canonical { get; set; }

    public string? Robots { get; set; }

    public DateTime UpdatedAt { get; set; }
}
