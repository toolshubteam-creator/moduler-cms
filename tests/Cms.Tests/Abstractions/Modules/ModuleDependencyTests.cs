namespace Cms.Tests.Abstractions.Modules;

using Cms.Abstractions.Modules;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;

public class ModuleDependencyTests
{
    [Fact]
    public void VersionRange_ParsesInclusiveExclusive_Correctly()
    {
        var range = VersionRange.Parse("[1.0.0, 2.0.0)");
        range.Satisfies(NuGetVersion.Parse("1.0.0")).Should().BeTrue();
        range.Satisfies(NuGetVersion.Parse("1.9.9")).Should().BeTrue();
        range.Satisfies(NuGetVersion.Parse("2.0.0")).Should().BeFalse();
    }

    [Fact]
    public void Defaults_IsOptionalIsFalse()
    {
        var dep = new ModuleDependency
        {
            ModuleId = "auth",
            VersionRange = VersionRange.Parse("[1.0.0, )")
        };
        dep.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void IsOptional_WhenSetTrue_IsTrue()
    {
        var dep = new ModuleDependency
        {
            ModuleId = "auth",
            VersionRange = VersionRange.Parse("[1.0.0, )"),
            IsOptional = true
        };
        dep.IsOptional.Should().BeTrue();
    }
}
