namespace Cms.Abstractions.Modules.Menu;

public sealed record MenuItem
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Url { get; init; }

    /// <summary>Goruntulemek icin gereken izin (opsiyonel).</summary>
    public string? RequiredPermission { get; init; }

    /// <summary>Sidebar'da sirayi belirler. Kucuk = once.</summary>
    public int Order { get; init; }

    /// <summary>Lucide / Heroicons isim. Render'da render edilir.</summary>
    public string? IconName { get; init; }
}
