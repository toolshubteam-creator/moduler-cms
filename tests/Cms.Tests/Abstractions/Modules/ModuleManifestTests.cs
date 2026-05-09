namespace Cms.Tests.Abstractions.Modules;

using Cms.Abstractions.Modules;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;

public class ModuleManifestTests
{
    private static ModuleManifest BuildSample(string id = "blog") => new()
    {
        Id = id,
        Name = "Blog Modulu",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0")
    };

    [Fact]
    public void Equality_TwoIdenticalManifests_AreEqual()
    {
        var a = BuildSample();
        var b = BuildSample();
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentIds_AreNotEqual()
    {
        var a = BuildSample("blog");
        var b = BuildSample("ecommerce");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Defaults_DependenciesIsEmpty()
    {
        var m = BuildSample();
        m.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_IsCorePluginIsFalse()
    {
        var m = BuildSample();
        m.IsCorePlugin.Should().BeFalse();
    }
}
