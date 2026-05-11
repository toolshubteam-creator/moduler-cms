namespace Cms.Modules.Blog.Services;

using Cms.Core.Data;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Microsoft.EntityFrameworkCore;

public sealed class TagService(TenantDbContext db) : ITagService
{
    public async Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugGenerator.Generate(request.Name)
            : SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos uretilemez.");
        }
        var slug = await EnsureUniqueSlugAsync(baseSlug, excludeId: null, cancellationToken).ConfigureAwait(false);

        var tag = new BlogTag { Name = request.Name.Trim(), Slug = slug };
        db.Set<BlogTag>().Add(tag);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task<TagDto> UpdateAsync(UpdateTagRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var tag = await db.Set<BlogTag>()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tag {request.Id} bulunamadi.");

        var baseSlug = SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos olamaz.");
        }
        var slug = baseSlug == tag.Slug
            ? tag.Slug
            : await EnsureUniqueSlugAsync(baseSlug, excludeId: tag.Id, cancellationToken).ConfigureAwait(false);

        tag.Name = request.Name.Trim();
        tag.Slug = slug;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await db.Set<BlogTag>()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tag {id} bulunamadi.");
        // BlogTag ISoftDeletable DEGIL → gercek Remove hard delete; FK cascade junction'i siler.
        db.Set<BlogTag>().Remove(tag);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TagDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await db.Set<BlogTag>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return tag is null ? null : new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task<TagDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var tag = await db.Set<BlogTag>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
        return tag is null ? null : new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tags = await db.Set<BlogTag>()
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. tags.Select(t => new TagDto(t.Id, t.Name, t.Slug))];
    }

    public async Task<IReadOnlyList<TagDto>> GetOrCreateManyAsync(
        IEnumerable<string> names,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        // 1) Isimleri trim + bos olmayan + slug uretilebilenler arasinda slug bazli tekillestir.
        var normalized = new List<(string Name, string Slug)>();
        var seenSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }
            var trimmed = raw.Trim();
            var slug = SlugGenerator.Generate(trimmed);
            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }
            if (seenSlugs.Add(slug))
            {
                normalized.Add((trimmed, slug));
            }
        }

        if (normalized.Count == 0)
        {
            return [];
        }

        var slugs = normalized.Select(n => n.Slug).ToList();
        var existing = await db.Set<BlogTag>()
            .Where(t => slugs.Contains(t.Slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingBySlug = existing.ToDictionary(t => t.Slug, StringComparer.Ordinal);

        var ordered = new List<BlogTag>();
        var added = new List<BlogTag>();
        foreach (var (name, slug) in normalized)
        {
            if (existingBySlug.TryGetValue(slug, out var hit))
            {
                ordered.Add(hit);
                continue;
            }
            var tag = new BlogTag { Name = name, Slug = slug };
            db.Set<BlogTag>().Add(tag);
            added.Add(tag);
            ordered.Add(tag);
        }

        if (added.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return [.. ordered.Select(t => new TagDto(t.Id, t.Name, t.Slug))];
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, int? excludeId, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Set<BlogTag>()
            .AsNoTracking()
            .AnyAsync(t => t.Slug == slug && (excludeId == null || t.Id != excludeId), cancellationToken)
            .ConfigureAwait(false))
        {
            slug = $"{baseSlug}-{suffix++}";
            if (slug.Length > 100)
            {
                slug = slug[..100];
            }
        }
        return slug;
    }
}
