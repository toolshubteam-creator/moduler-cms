namespace Cms.Modules.Blog.Services;

using Cms.Core.Data;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Microsoft.EntityFrameworkCore;

public sealed class CategoryService(TenantDbContext db) : ICategoryService
{
    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        if (request.ParentCategoryId is int parentId)
        {
            var exists = await db.Set<BlogCategory>()
                .AsNoTracking()
                .AnyAsync(c => c.Id == parentId, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                throw new InvalidOperationException($"ParentCategoryId {parentId} bulunamadi.");
            }
        }

        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugGenerator.Generate(request.Name)
            : SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos uretilemez (name de bos veya sadece ozel karakter).");
        }
        var slug = await EnsureUniqueSlugAsync(baseSlug, excludeId: null, cancellationToken).ConfigureAwait(false);

        var cat = new BlogCategory
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = Normalize(request.Description),
            ParentCategoryId = request.ParentCategoryId,
        };
        db.Set<BlogCategory>().Add(cat);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(cat);
    }

    public async Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var cat = await db.Set<BlogCategory>()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Category {request.Id} bulunamadi.");

        if (request.ParentCategoryId is int newParent)
        {
            if (newParent == request.Id)
            {
                throw new InvalidOperationException("Kategori kendi parent'i olamaz.");
            }
            var parentExists = await db.Set<BlogCategory>()
                .AsNoTracking()
                .AnyAsync(c => c.Id == newParent, cancellationToken)
                .ConfigureAwait(false);
            if (!parentExists)
            {
                throw new InvalidOperationException($"ParentCategoryId {newParent} bulunamadi.");
            }
            await EnsureNoCycleAsync(request.Id, newParent, cancellationToken).ConfigureAwait(false);
        }

        var baseSlug = SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos olamaz.");
        }
        var slug = baseSlug == cat.Slug
            ? cat.Slug
            : await EnsureUniqueSlugAsync(baseSlug, excludeId: cat.Id, cancellationToken).ConfigureAwait(false);

        cat.Name = request.Name.Trim();
        cat.Slug = slug;
        cat.Description = Normalize(request.Description);
        cat.ParentCategoryId = request.ParentCategoryId;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(cat);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var hasChildren = await db.Set<BlogCategory>()
            .AsNoTracking()
            .AnyAsync(c => c.ParentCategoryId == id, cancellationToken)
            .ConfigureAwait(false);
        if (hasChildren)
        {
            throw new InvalidOperationException(
                "Bu kategorinin alt kategorileri var. Once onlari tasiyin veya silin.");
        }
        var cat = await db.Set<BlogCategory>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Category {id} bulunamadi.");
        db.Set<BlogCategory>().Remove(cat);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var cat = await db.Set<BlogCategory>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Soft-deleted category {id} bulunamadi.");
        cat.IsDeleted = false;
        cat.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CategoryDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var cat = await db.Set<BlogCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return cat is null ? null : ToDto(cat);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var cats = await db.Set<BlogCategory>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. cats.Select(ToDto)];
    }

    public async Task<IReadOnlyList<CategoryTreeNodeDto>> ListWithIndentAsync(CancellationToken cancellationToken = default)
    {
        var all = await db.Set<BlogCategory>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byParent = all
            .GroupBy(c => c.ParentCategoryId ?? -1)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<CategoryTreeNodeDto>();
        Walk(byParent, parentKey: -1, depth: 0, result);
        return result;
    }

    private static void Walk(
        Dictionary<int, List<BlogCategory>> byParent,
        int parentKey,
        int depth,
        List<CategoryTreeNodeDto> result)
    {
        if (!byParent.TryGetValue(parentKey, out var children))
        {
            return;
        }
        foreach (var c in children)
        {
            result.Add(new CategoryTreeNodeDto(c.Id, c.Name, c.Slug, c.Description, c.ParentCategoryId, depth));
            Walk(byParent, c.Id, depth + 1, result);
        }
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, int? excludeId, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Set<BlogCategory>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug && (excludeId == null || c.Id != excludeId), cancellationToken)
            .ConfigureAwait(false))
        {
            slug = $"{baseSlug}-{suffix++}";
            if (slug.Length > 200)
            {
                slug = slug[..200];
            }
        }
        return slug;
    }

    private async Task EnsureNoCycleAsync(int categoryId, int candidateParentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<int> { categoryId };
        var cursor = (int?)candidateParentId;
        while (cursor is int cur)
        {
            if (!visited.Add(cur))
            {
                throw new InvalidOperationException("Kategori dongusu tespit edildi (cycle).");
            }
            cursor = await db.Set<BlogCategory>()
                .AsNoTracking()
                .Where(c => c.Id == cur)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CategoryDto ToDto(BlogCategory c) =>
        new(c.Id, c.Name, c.Slug, c.Description, c.ParentCategoryId);
}
