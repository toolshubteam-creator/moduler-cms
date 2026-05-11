namespace Cms.Modules.Media.Services;

using Cms.Core.Data;
using Cms.Core.Tenancy;
using Cms.Modules.Media.Contracts;
using Cms.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;

public sealed class MediaService(
    TenantDbContext db,
    IFileStorage storage,
    ITenantContext tenant) : IMediaService
{
    public async Task<MediaFile> UploadAsync(
        Stream content,
        string fileName,
        string mimeType,
        string? altText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        if (!tenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant resolve edilmeden upload yapilamaz.");
        }
        var tenantId = tenant.Current!.Id;
        var ext = Path.GetExtension(fileName);

        var storageResult = await storage.SaveAsync(content, ext, tenantId, cancellationToken).ConfigureAwait(false);

        // B yaklasimi: ayni hash icin disk'te tek dosya, DB'de HER zaman yeni satir.
        var entity = new MediaFileEntity
        {
            FileName = fileName,
            StoredPath = storageResult.StoredPath,
            MimeType = mimeType,
            SizeBytes = storageResult.SizeBytes,
            Hash = storageResult.Hash,
            AltText = string.IsNullOrWhiteSpace(altText) ? null : altText,
            UploadedAt = DateTime.UtcNow,
        };
        db.Set<MediaFileEntity>().Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToContract(entity);
    }

    public async Task<MediaFile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Set<MediaFileEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToContract(entity);
    }

    public async Task<Stream?> OpenContentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Set<MediaFileEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }
        return await storage.OpenReadAsync(entity.StoredPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MediaFile>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            skip = 0;
        }
        if (take <= 0)
        {
            take = 50;
        }

        var entities = await db.Set<MediaFileEntity>()
            .AsNoTracking()
            .OrderByDescending(e => e.UploadedAt)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. entities.Select(ToContract)];
    }

    public async Task UpdateMetadataAsync(int id, string? altText, CancellationToken cancellationToken = default)
    {
        var entity = await db.Set<MediaFileEntity>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }
        entity.AltText = string.IsNullOrWhiteSpace(altText) ? null : altText;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Set<MediaFileEntity>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }
        db.Set<MediaFileEntity>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static MediaFile ToContract(MediaFileEntity e) =>
        new(e.Id, e.FileName, e.StoredPath, e.MimeType, e.SizeBytes, e.Hash, e.AltText, e.UploadedAt);
}
