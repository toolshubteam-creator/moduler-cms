namespace Cms.Modules.Blog.Contracts;

public interface IPostService
{
    Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default);

    Task<PostDto> UpdateAsync(UpdatePostRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (ISoftDeletable). Disk/SEO temizleme yapilmaz.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deleted post'u geri yukle.</summary>
    Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task<PostDto> PublishAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Draft'a dondurur. PublishedAt KORUNUR (yayin gecmisi tarihi).</summary>
    Task<PostDto> UnpublishAsync(int id, CancellationToken cancellationToken = default);

    Task<PostDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<PostDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostDto>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);
}
