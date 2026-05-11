namespace Cms.Modules.Blog.Services;

using Cms.Core.Data;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Cms.Modules.Media.Contracts;
using Microsoft.EntityFrameworkCore;

public sealed class PostService(TenantDbContext db, IMediaService mediaService) : IPostService
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
        return ToDto(post);
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
        return ToDto(post);
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
        return ToDto(post);
    }

    public async Task<PostDto> UnpublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Post {id} bulunamadi.");
        post.Status = PostStatus.Draft;
        // PublishedAt KORUNUR — yayin gecmisi tarihi.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(post);
    }

    public async Task<PostDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await db.Set<BlogPost>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        return post is null ? null : ToDto(post);
    }

    public async Task<PostDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var post = await db.Set<BlogPost>().AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken).ConfigureAwait(false);
        return post is null ? null : ToDto(post);
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
        return [.. posts.Select(ToDto)];
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PostDto ToDto(BlogPost p) =>
        new(p.Id, p.Title, p.Slug, p.Excerpt, p.Content, p.Status, p.PublishAt, p.PublishedAt, p.FeaturedMediaId, p.AuthorUserId);
}
