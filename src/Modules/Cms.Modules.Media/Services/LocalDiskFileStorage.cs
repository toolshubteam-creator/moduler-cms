namespace Cms.Modules.Media.Services;

using System.Globalization;
using System.Security.Cryptography;
using Cms.Modules.Media.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

public sealed class LocalDiskFileStorage(
    IOptions<MediaStorageOptions> options,
    IWebHostEnvironment env) : IFileStorage
{
    private readonly MediaStorageOptions _options = options.Value;
    private readonly IWebHostEnvironment _env = env;

    public async Task<FileStorageResult> SaveAsync(
        Stream content,
        string extension,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // SHA256 streaming yerine memory'e cek — 50MB limit altinda kabul edilebilir.
        // Faz-4.3+ streaming hash + temp file optimizasyonu dusunuluyor.
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var size = ms.Length;
        ms.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(ms, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        ms.Position = 0;

        var now = DateTime.UtcNow;
        var ext = NormalizeExtension(extension);
        var relativePath = string.Create(CultureInfo.InvariantCulture,
            $"{tenantId}/{now:yyyy}/{now:MM}/{hash}{ext}");

        var absolutePath = ResolveAbsolute(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        if (!File.Exists(absolutePath))
        {
            await using var fs = File.Create(absolutePath);
            await ms.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        return new FileStorageResult(relativePath, hash, size);
    }

    public Task<Stream?> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedPath);
        var absolute = ResolveAbsolute(storedPath);
        if (!File.Exists(absolute))
        {
            return Task.FromResult<Stream?>(null);
        }
        Stream stream = File.OpenRead(absolute);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedPath);
        return Task.FromResult(File.Exists(ResolveAbsolute(storedPath)));
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedPath);
        var absolute = ResolveAbsolute(storedPath);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }
        return Task.CompletedTask;
    }

    private string ResolveAbsolute(string relative)
    {
        var root = Path.IsPathRooted(_options.StoragePath)
            ? _options.StoragePath
            : Path.Combine(_env.ContentRootPath, _options.StoragePath);
        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }
        var ext = extension.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}
