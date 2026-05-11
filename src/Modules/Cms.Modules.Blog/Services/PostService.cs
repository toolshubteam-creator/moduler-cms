namespace Cms.Modules.Blog.Services;

using Cms.Core.Data;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Cms.Modules.Media.Contracts;
using Microsoft.EntityFrameworkCore;

public sealed class PostService(
    TenantDbContext db,
    IMediaService mediaService,
    ITagService tagService) : IPostService
{
    public async Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);

        await ValidateMediaAsync(request.FeaturedMediaId, cancellationToken).ConfigureAwait(false);

        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugGenerator.Generate(request.Title)
            : SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos uretilemez (title de bos veya sadece ozel karakter).");
        }
        var slug = await EnsureUniqueSlugAsync(baseSlug, excludeId: null, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var post = new BlogPost
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Excerpt = Normalize(request.Excerpt),
            Content = request.Content,
            Status = request.Status,
            PublishAt = request.PublishAt,
            PublishedAt = request.Status == PostStatus.Published ? now : null,
            FeaturedMediaId = request.FeaturedMediaId,
            AuthorUserId = request.AuthorUserId,
        };
        db.Set<BlogPost>().Add(post);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await SyncCategoriesAsync(post.Id, request.CategoryIds, cancellationToken).ConfigureAwait(false);
        await SyncTagsAsync(post.Id, request.TagNames, cancellationToken).ConfigureAwait(false);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PostDto> UpdateAsync(UpdatePostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var post = await db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Post {request.Id} bulunamadi.");

        await ValidateMediaAsync(request.FeaturedMediaId, cancellationToken).ConfigureAwait(false);

        var baseSlug = SlugGenerator.Generate(request.Slug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new InvalidOperationException("Slug bos olamaz.");
        }
        var slug = baseSlug == post.Slug
            ? post.Slug
            : await EnsureUniqueSlugAsync(baseSlug, excludeId: post.Id, cancellationToken).ConfigureAwait(false);

        post.Title = request.Title.Trim();
        post.Slug = slug;
        post.Excerpt = Normalize(request.Excerpt);
        post.Content = request.Content;
        post.Status = request.Status;
        post.PublishAt = request.PublishAt;
        post.FeaturedMediaId = request.FeaturedMediaId;
        if (request.Status == PostStatus.Published && post.PublishedAt is null)
        {
            post.PublishedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await SyncCategoriesAsync(post.Id, request.CategoryIds, cancellationToken).ConfigureAwait(false);
        await SyncTagsAsync(post.Id, request.TagNames, cancellationToken).ConfigureAwait(false);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        if (post is null)
        {
            return false;
        }
        db.Set<BlogPost>().Remove(post);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (post is null)
        {
            return false;
        }
        post.IsDeleted = false;
        post.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<PostDto> PublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Post {id} bulunamadi.");
        post.Status = PostStatus.Published;
        post.PublishedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PostDto> UnpublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Post {id} bulunamadi.");
        post.Status = PostStatus.Draft;
        // PublishedAt KORUNUR — yayin gecmisi tarihi.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PostDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        return post is null ? null : await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PostDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var post = await db.Set<BlogPost>().AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken).ConfigureAwait(false);
        return post is null ? null : await ToDtoAsync(post, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PostDto>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            skip = 0;
        }
        if (take <= 0)
        {
            take = 50;
        }
        var posts = await db.Set<BlogPost>()
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (posts.Count == 0)
        {
            return [];
        }

        var ids = posts.Select(p => p.Id).ToList();

        // Batch fetch ile N+1 once: tum post'lar icin kategori + tag bilgisini tek query'de cek.
        var catLinks = await db.Set<BlogPostCategory>()
            .AsNoTracking()
            .Where(pc => ids.Contains(pc.PostId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var catByPost = catLinks
            .GroupBy(pc => pc.PostId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)[.. g.Select(x => x.CategoryId)]);

        var tagJoin = await (
            from pt in db.Set<BlogPostTag>().AsNoTracking()
            join t in db.Set<BlogTag>().AsNoTracking() on pt.TagId equals t.Id
            where ids.Contains(pt.PostId)
            select new { pt.PostId, t.Name }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var tagsByPost = tagJoin
            .GroupBy(x => x.PostId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(x => x.Name)]);

        var result = new List<PostDto>(posts.Count);
        foreach (var p in posts)
        {
            var cats = catByPost.TryGetValue(p.Id, out var cs) ? cs : [];
            var tags = tagsByPost.TryGetValue(p.Id, out var ts) ? ts : [];
            result.Add(new PostDto(
                p.Id, p.Title, p.Slug, p.Excerpt, p.Content, p.Status,
                p.PublishAt, p.PublishedAt, p.FeaturedMediaId, p.AuthorUserId,
                cats, tags));
        }
        return result;
    }

    private async Task ValidateMediaAsync(int? mediaId, CancellationToken cancellationToken)
    {
        if (mediaId is null)
        {
            return;
        }
        var media = await mediaService.GetByIdAsync(mediaId.Value, cancellationToken).ConfigureAwait(false);
        if (media is null)
        {
            throw new InvalidOperationException($"FeaturedMediaId {mediaId} bulunamadi.");
        }
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, int? excludeId, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Set<BlogPost>()
            .AsNoTracking()
            .AnyAsync(p => p.Slug == slug && (excludeId == null || p.Id != excludeId), cancellationToken)
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

    private async Task SyncCategoriesAsync(int postId, IReadOnlyList<int> categoryIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);

        var existing = await db.Set<BlogPostCategory>()
            .Where(pc => pc.PostId == postId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targetIds = categoryIds.Distinct().ToList();
        if (targetIds.Count > 0)
        {
            // Verilen kategori id'leri tenant'ta var mi? (Soft-deleted hari)
            var validIds = await db.Set<BlogCategory>()
                .AsNoTracking()
                .Where(c => targetIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var missing = targetIds.Except(validIds).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Kategori id(ler)i bulunamadi: {string.Join(", ", missing)}.");
            }
        }

        var toRemove = existing.Where(e => !targetIds.Contains(e.CategoryId)).ToList();
        var existingIds = existing.Select(e => e.CategoryId).ToHashSet();
        var toAdd = targetIds.Where(id => !existingIds.Contains(id)).ToList();

        if (toRemove.Count > 0)
        {
            db.Set<BlogPostCategory>().RemoveRange(toRemove);
        }
        foreach (var catId in toAdd)
        {
            db.Set<BlogPostCategory>().Add(new BlogPostCategory { PostId = postId, CategoryId = catId });
        }
    }

    private async Task SyncTagsAsync(int postId, IReadOnlyList<string> tagNames, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tagNames);

        var tags = await tagService.GetOrCreateManyAsync(tagNames, cancellationToken).ConfigureAwait(false);
        var tagIds = tags.Select(t => t.Id).ToList();

        var existing = await db.Set<BlogPostTag>()
            .Where(pt => pt.PostId == postId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRemove = existing.Where(e => !tagIds.Contains(e.TagId)).ToList();
        var existingIds = existing.Select(e => e.TagId).ToHashSet();
        var toAdd = tagIds.Where(id => !existingIds.Contains(id)).ToList();

        if (toRemove.Count > 0)
        {
            db.Set<BlogPostTag>().RemoveRange(toRemove);
        }
        foreach (var tagId in toAdd)
        {
            db.Set<BlogPostTag>().Add(new BlogPostTag { PostId = postId, TagId = tagId });
        }
    }

    private async Task<PostDto> ToDtoAsync(BlogPost p, CancellationToken cancellationToken)
    {
        var categoryIds = await db.Set<BlogPostCategory>()
            .AsNoTracking()
            .Where(pc => pc.PostId == p.Id)
            .Select(pc => pc.CategoryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tagNames = await (
            from pt in db.Set<BlogPostTag>().AsNoTracking()
            join t in db.Set<BlogTag>().AsNoTracking() on pt.TagId equals t.Id
            where pt.PostId == p.Id
            select t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PostDto(
            p.Id, p.Title, p.Slug, p.Excerpt, p.Content, p.Status,
            p.PublishAt, p.PublishedAt, p.FeaturedMediaId, p.AuthorUserId,
            categoryIds, tagNames);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
