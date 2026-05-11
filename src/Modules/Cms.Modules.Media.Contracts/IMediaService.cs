namespace Cms.Modules.Media.Contracts;

public interface IMediaService
{
    Task<MediaFile> UploadAsync(Stream content, string fileName, string mimeType, string? altText, CancellationToken cancellationToken = default);

    Task<MediaFile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Stream?> OpenContentAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFile>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    Task UpdateMetadataAsync(int id, string? altText, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (ISoftDeletable). Disk'teki fiziksel dosya KALIR (paylasimli olabilir).</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record MediaFile(
    int Id,
    string FileName,
    string StoredPath,
    string MimeType,
    long SizeBytes,
    string Hash,
    string? AltText,
    DateTime UploadedAt);
