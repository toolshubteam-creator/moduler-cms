namespace Cms.Tests.Modules.Media;

using System.Security.Cryptography;
using System.Text;
using Cms.Modules.Media;
using Cms.Modules.Media.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

public class LocalDiskFileStorageTests : IDisposable
{
    private readonly string _tempRoot;

    public LocalDiskFileStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"cms-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* test cleanup */ }
        GC.SuppressFinalize(this);
    }

    private LocalDiskFileStorage BuildSut()
    {
        var opts = Options.Create(new MediaStorageOptions { StoragePath = _tempRoot });
        var env = new FakeWebHostEnvironment { ContentRootPath = _tempRoot };
        return new LocalDiskFileStorage(opts, env);
    }

    [Fact]
    public async Task SaveAsync_WritesFileToDisk_AndComputesSha256()
    {
        var sut = BuildSut();
        var tenantId = Guid.NewGuid();
        var content = "hello world"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await sut.SaveAsync(stream, ".txt", tenantId);

        result.SizeBytes.Should().Be(content.Length);
        var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        result.Hash.Should().Be(expectedHash);

        var absolute = Path.Combine(_tempRoot, result.StoredPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(absolute).Should().BeTrue();
        (await File.ReadAllBytesAsync(absolute)).Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task SaveAsync_SameContentTwice_KeepsSingleFileOnDisk()
    {
        var sut = BuildSut();
        var tenantId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("idempotent payload");

        FileStorageResultLike first;
        FileStorageResultLike second;
        using (var s1 = new MemoryStream(content))
        {
            var r1 = await sut.SaveAsync(s1, ".bin", tenantId);
            first = new FileStorageResultLike(r1.StoredPath, r1.Hash);
        }
        using (var s2 = new MemoryStream(content))
        {
            var r2 = await sut.SaveAsync(s2, ".bin", tenantId);
            second = new FileStorageResultLike(r2.StoredPath, r2.Hash);
        }

        first.Hash.Should().Be(second.Hash);
        first.StoredPath.Should().Be(second.StoredPath);

        var dir = Path.GetDirectoryName(Path.Combine(_tempRoot, first.StoredPath.Replace('/', Path.DirectorySeparatorChar)))!;
        var files = Directory.GetFiles(dir);
        files.Should().HaveCount(1, "ayni hash icin disk'te tek dosya");
    }

    [Fact]
    public async Task SaveAsync_DifferentTenants_GoIntoSeparateFolders()
    {
        var sut = BuildSut();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var content = "tenant-isolation"u8.ToArray();

        using var s1 = new MemoryStream(content);
        var r1 = await sut.SaveAsync(s1, ".dat", t1);
        using var s2 = new MemoryStream(content);
        var r2 = await sut.SaveAsync(s2, ".dat", t2);

        r1.StoredPath.Should().StartWith(t1.ToString());
        r2.StoredPath.Should().StartWith(t2.ToString());
        r1.Hash.Should().Be(r2.Hash);
        r1.StoredPath.Should().NotBe(r2.StoredPath);
    }

    [Fact]
    public async Task OpenReadAsync_ExistingFile_ReturnsStreamWithExpectedBytes()
    {
        var sut = BuildSut();
        var content = "round-trip"u8.ToArray();
        using var s = new MemoryStream(content);
        var saved = await sut.SaveAsync(s, ".txt", Guid.NewGuid());

        await using var read = await sut.OpenReadAsync(saved.StoredPath);
        read.Should().NotBeNull();
        using var copy = new MemoryStream();
        await read!.CopyToAsync(copy);
        copy.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task OpenReadAsync_MissingFile_ReturnsNull()
    {
        var sut = BuildSut();
        var s = await sut.OpenReadAsync("no/such/file.bin");
        s.Should().BeNull();
    }

    private sealed record FileStorageResultLike(string StoredPath, string Hash);

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Cms.Tests";
    }
}
