namespace Cms.Tests.Core.Modules;

using System.Reflection;
using Cms.Abstractions.Modules;
using Cms.Core.Modules;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;

public class ModuleDependencyResolverTests
{
    private static ModuleDescriptor BuildDescriptor(
        string id,
        string version = "1.0.0",
        bool isCore = false,
        params (string id, string range, bool optional)[] deps)
    {
        var manifest = new ModuleManifest
        {
            Id = id,
            Name = id,
            Version = NuGetVersion.Parse(version),
            MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
            IsCorePlugin = isCore,
            Dependencies = deps.Select(d => new ModuleDependency
            {
                ModuleId = d.id,
                VersionRange = VersionRange.Parse(d.range),
                IsOptional = d.optional
            }).ToList()
        };
        var stub = new StubModule(manifest);
        return new ModuleDescriptor
        {
            Instance = stub,
            Assembly = Assembly.GetExecutingAssembly(),
            DllPath = $"{id}.dll"
        };
    }

    private sealed class StubModule(ModuleManifest manifest) : ModuleBase
    {
        public override ModuleManifest Manifest { get; } = manifest;
    }

    [Fact]
    public void Resolve_NoDependencies_ReturnsAllInIdOrder()
    {
        var modules = new[] { BuildDescriptor("blog"), BuildDescriptor("auth") };

        var sorted = ModuleDependencyResolver.Resolve(modules);

        sorted.Select(m => m.Manifest.Id).Should().ContainInOrder("auth", "blog");
    }

    [Fact]
    public void Resolve_CorePluginsBeforeRegular()
    {
        var modules = new[]
        {
            BuildDescriptor("blog"),
            BuildDescriptor("auth", isCore: true)
        };

        var sorted = ModuleDependencyResolver.Resolve(modules);

        sorted[0].Manifest.Id.Should().Be("auth");
        sorted[1].Manifest.Id.Should().Be("blog");
    }

    [Fact]
    public void Resolve_DependentLoadsAfterDependency()
    {
        var modules = new[]
        {
            BuildDescriptor("blog", deps: ("auth", "[1.0.0, )", false)),
            BuildDescriptor("auth")
        };

        var sorted = ModuleDependencyResolver.Resolve(modules);

        var authIndex = sorted.ToList().FindIndex(m => m.Manifest.Id == "auth");
        var blogIndex = sorted.ToList().FindIndex(m => m.Manifest.Id == "blog");
        authIndex.Should().BeLessThan(blogIndex);
    }

    [Fact]
    public void Resolve_MissingMandatoryDependency_Throws()
    {
        var modules = new[]
        {
            BuildDescriptor("blog", deps: ("auth", "[1.0.0, )", false))
        };

        var act = () => ModuleDependencyResolver.Resolve(modules);

        act.Should().Throw<InvalidOperationException>().WithMessage("*auth*bulunamadi*");
    }

    [Fact]
    public void Resolve_MissingOptionalDependency_DoesNotThrow()
    {
        var modules = new[]
        {
            BuildDescriptor("blog", deps: ("auth", "[1.0.0, )", true))
        };

        var act = () => ModuleDependencyResolver.Resolve(modules);

        act.Should().NotThrow();
    }

    [Fact]
    public void Resolve_VersionMismatch_Throws()
    {
        var modules = new[]
        {
            BuildDescriptor("blog", deps: ("auth", "[2.0.0, )", false)),
            BuildDescriptor("auth", "1.0.0")
        };

        var act = () => ModuleDependencyResolver.Resolve(modules);

        act.Should().Throw<InvalidOperationException>().WithMessage("*auth*2.0.0*1.0.0*");
    }

    [Fact]
    public void Resolve_Cycle_Throws()
    {
        var modules = new[]
        {
            BuildDescriptor("a", deps: ("b", "[1.0.0, )", false)),
            BuildDescriptor("b", deps: ("a", "[1.0.0, )", false))
        };

        var act = () => ModuleDependencyResolver.Resolve(modules);

        act.Should().Throw<InvalidOperationException>().WithMessage("*dongusu*");
    }
}
