namespace Cms.Modules.Media.Contracts;

/// <summary>
/// Hash-based content-addressable storage. Ayni icerik (ayni hash) icin disk'te
/// tek dosya tutulur — caller'lar farkli metadata kayitlari yapsa bile fiziksel
/// dosya idempotent paylasilir. <see cref="SaveAsync"/> File.Exists kontrolu ile
/// re-write yapmaz.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Stream icerigi SHA256 ile hashlenir, <c>{StoragePath}/{tenantId}/{yyyy}/{MM}/{hash}.{ext}</c>
    /// formatinda diske yazilir. Ayni hash + path mevcutsa idempotent return (yeniden yazma yok).
    /// </summary>
    Task<FileStorageResult> SaveAsync(Stream content, string extension, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>StoredPath relative (tenantId/yyyy/MM/hash.ext); dosya yoksa null.</summary>
    Task<Stream?> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storedPath, CancellationToken cancellationToken = default);

    /// <summary>HARD delete — fiziksel dosyayi siler. Soft-delete akisinda CAGRILMAZ.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}

public sealed record FileStorageResult(string StoredPath, string Hash, long SizeBytes);
