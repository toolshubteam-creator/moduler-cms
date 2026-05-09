namespace Cms.Tests.FakeModule;

using Cms.Abstractions.Modules;
using NuGet.Versioning;

public sealed class FakeModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "fake",
        Name = "Fake Test Modulu",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "ModuleLoader integration testi icin"
    };
}
