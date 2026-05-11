namespace Cms.Modules.Seo.Contracts;

public interface ISeoMetaService
{
    Task<SeoMeta?> GetAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>Upsert: mevcut (TargetType, TargetId) varsa Update, yoksa Insert. Audit Update/Create yazilir.</summary>
    Task<SeoMeta> SetAsync(string targetType, string targetId, SeoMetaInput input, CancellationToken cancellationToken = default);

    /// <summary>Hard delete (ISoftDeletable yok — yeni meta yazilirsa eskinin izi audit'te kalir).</summary>
    Task<bool> DeleteAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeoMeta>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Render zamani: meta varsa onu, eksik alanlari Settings'ten default ("seo.default_*")
    /// degerleri ile doldurarak donder. Hicbiri yoksa tum alanlar null.
    /// </summary>
    Task<SeoMetaResolved> ResolveAsync(string targetType, string targetId, CancellationToken cancellationToken = default);
}

public sealed record SeoMeta(
    int Id,
    string TargetType,
    string TargetId,
    string? Title,
    string? Description,
    string? OgImage,
    string? Canonical,
    string? Robots,
    DateTime UpdatedAt);

public sealed record SeoMetaInput(
    string? Title,
    string? Description,
    string? OgImage,
    string? Canonical,
    string? Robots);

public sealed record SeoMetaResolved(
    string? Title,
    string? Description,
    string? OgImage,
    string? Canonical,
    string? Robots);
