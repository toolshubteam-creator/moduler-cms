namespace Cms.Modules.Seo.Services;

using Cms.Core.Data;
using Cms.Modules.Seo.Contracts;
using Cms.Modules.Seo.Domain;
using Cms.Modules.Settings.Contracts;
using Microsoft.EntityFrameworkCore;

public sealed class SeoMetaService(TenantDbContext db, ISettingsService settings) : ISeoMetaService
{
    private const string DefaultTitleKey = "seo.default_title";
    private const string DefaultDescriptionKey = "seo.default_description";
    private const string DefaultOgImageKey = "seo.default_og_image";
    private const string DefaultCanonicalKey = "seo.default_canonical";
    private const string DefaultRobotsKey = "seo.default_robots";

    public async Task<SeoMeta?> GetAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        var entity = await db.Set<SeoMetaEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TargetType == targetType && e.TargetId == targetId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToContract(entity);
    }

    public async Task<SeoMeta> SetAsync(string targetType, string targetId, SeoMetaInput input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(input);

        var existing = await db.Set<SeoMetaEntity>()
            .FirstOrDefaultAsync(e => e.TargetType == targetType && e.TargetId == targetId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new SeoMetaEntity
            {
                TargetType = targetType,
                TargetId = targetId,
                Title = Normalize(input.Title),
                Description = Normalize(input.Description),
                OgImage = Normalize(input.OgImage),
                Canonical = Normalize(input.Canonical),
                Robots = Normalize(input.Robots),
                UpdatedAt = now,
            };
            db.Set<SeoMetaEntity>().Add(existing);
        }
        else
        {
            existing.Title = Normalize(input.Title);
            existing.Description = Normalize(input.Description);
            existing.OgImage = Normalize(input.OgImage);
            existing.Canonical = Normalize(input.Canonical);
            existing.Robots = Normalize(input.Robots);
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToContract(existing);
    }

    public async Task<bool> DeleteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        var existing = await db.Set<SeoMetaEntity>()
            .FirstOrDefaultAsync(e => e.TargetType == targetType && e.TargetId == targetId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }
        db.Set<SeoMetaEntity>().Remove(existing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<SeoMeta>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            skip = 0;
        }
        if (take <= 0)
        {
            take = 50;
        }

        var entities = await db.Set<SeoMetaEntity>()
            .AsNoTracking()
            .OrderBy(e => e.TargetType).ThenBy(e => e.TargetId)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. entities.Select(ToContract)];
    }

    public async Task<SeoMetaResolved> ResolveAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        var meta = await GetAsync(targetType, targetId, cancellationToken).ConfigureAwait(false);

        var defaultTitle = await settings.GetAsync<string>(DefaultTitleKey, cancellationToken).ConfigureAwait(false);
        var defaultDescription = await settings.GetAsync<string>(DefaultDescriptionKey, cancellationToken).ConfigureAwait(false);
        var defaultOgImage = await settings.GetAsync<string>(DefaultOgImageKey, cancellationToken).ConfigureAwait(false);
        var defaultCanonical = await settings.GetAsync<string>(DefaultCanonicalKey, cancellationToken).ConfigureAwait(false);
        var defaultRobots = await settings.GetAsync<string>(DefaultRobotsKey, cancellationToken).ConfigureAwait(false);

        return new SeoMetaResolved(
            meta?.Title ?? defaultTitle,
            meta?.Description ?? defaultDescription,
            meta?.OgImage ?? defaultOgImage,
            meta?.Canonical ?? defaultCanonical,
            meta?.Robots ?? defaultRobots);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SeoMeta ToContract(SeoMetaEntity e) =>
        new(e.Id, e.TargetType, e.TargetId, e.Title, e.Description, e.OgImage, e.Canonical, e.Robots, e.UpdatedAt);
}
