namespace Cms.Abstractions.Modules;

using NuGet.Versioning;

/// <summary>
/// Bir modulun baska bir modulun belirli surumune olan bagimliligi.
/// </summary>
public sealed record ModuleDependency
{
    public required string ModuleId { get; init; }

    /// <summary>Kabul edilen surum araligi. Ornek: "[1.0.0, 2.0.0)" = 1.x.</summary>
    public required VersionRange VersionRange { get; init; }

    /// <summary>Bagimlilik opsiyonel mi? Default: false (zorunlu).</summary>
    public bool IsOptional { get; init; }
}
