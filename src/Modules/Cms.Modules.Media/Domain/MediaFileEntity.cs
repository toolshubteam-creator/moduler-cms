namespace Cms.Modules.Media.Domain;

using Cms.Core.Domain.Auditing;

public sealed class MediaFileEntity : IAuditable, ISoftDeletable
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Disk root'a relative — `{tenantId}/{yyyy}/{MM}/{hash}.{ext}` format.</summary>
    public string StoredPath { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA256 hex lowercase (64 char).</summary>
    public string Hash { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public DateTime UploadedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
