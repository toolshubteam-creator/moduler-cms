namespace Cms.Modules.Blog.Contracts;

public interface ITagService
{
    Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default);

    Task<TagDto> UpdateAsync(UpdateTagRequest request, CancellationToken cancellationToken = default);

    /// <summary>Hard delete. Blog_PostTags junction'i FK CASCADE ile temizlenir.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<TagDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<TagDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Free-text giris (virgulle ayrilmis isim listesi) icin: trim + dedup +
    /// slug bazli mevcut tag reuse + olmayanlari yarat.
    /// </summary>
    Task<IReadOnlyList<TagDto>> GetOrCreateManyAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
}
