namespace Cms.Tests.Core.Modules;

using Cms.Core.Modules;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class ModuleLoaderIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleLoaderIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cms-modules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var fakeDll = Path.Combine(AppContext.BaseDirectory, "Cms.Tests.FakeModule.dll");
        if (File.Exists(fakeDll))
        {
            File.Copy(fakeDll, Path.Combine(_tempDir, "Cms.Tests.FakeModule.dll"));
        }
    }

    [Fact]
    public void LoadAll_FindsFakeModule_ReturnsDescriptorWithCorrectManifest()
    {
        var options = Options.Create(new ModuleLoaderOptions
        {
            Path = _tempDir,
            SearchPattern = "*.dll"
        });
        var loader = new ModuleLoader(options, NullLogger<ModuleLoader>.Instance);

        var modules = loader.LoadAll();

        modules.Should().HaveCount(1);
        modules[0].Manifest.Id.Should().Be("fake");
        modules[0].Manifest.Name.Should().Be("Fake Test Modulu");
        modules[0].Manifest.Version.ToNormalizedString().Should().Be("1.0.0");
    }

    [Fact]
    public void LoadAll_NonExistentPath_ReturnsEmpty()
    {
        var options = Options.Create(new ModuleLoaderOptions
        {
            Path = Path.Combine(_tempDir, "yok-boyle-bir-yer"),
            SearchPattern = "*.dll"
        });
        var loader = new ModuleLoader(options, NullLogger<ModuleLoader>.Instance);

        var modules = loader.LoadAll();

        modules.Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
