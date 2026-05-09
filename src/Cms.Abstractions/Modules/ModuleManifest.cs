namespace Cms.Abstractions.Modules;

using NuGet.Versioning;

/// <summary>
/// Bir modulun kimlik kartidir. Her modul bir Manifest dondurur.
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>Benzersiz modul kimligi, snake_case veya kebab-case. Ornek: "blog", "ecommerce".</summary>
    public required string Id { get; init; }

    /// <summary>Insan-okur ad. Ornek: "Blog Modulu".</summary>
    public required string Name { get; init; }

    /// <summary>SemVer modul surumu.</summary>
    public required NuGetVersion Version { get; init; }

    /// <summary>Modulun gerektirdigi minimum cekirdek surumu.</summary>
    public required NuGetVersion MinimumCoreVersion { get; init; }

    /// <summary>Diger modul bagimliliklari. Bos liste = bagimsiz.</summary>
    public IReadOnlyList<ModuleDependency> Dependencies { get; init; } = [];

    /// <summary>Aciklama metni. Opsiyonel.</summary>
    public string? Description { get; init; }

    /// <summary>Yazar / saglayici. Opsiyonel.</summary>
    public string? Author { get; init; }

    /// <summary>Cekirdek modul mu (Auth, Tenancy gibi)? Default: false.</summary>
    public bool IsCorePlugin { get; init; }
}
