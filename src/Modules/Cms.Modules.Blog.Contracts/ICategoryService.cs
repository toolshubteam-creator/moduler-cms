namespace Cms.Modules.Blog.Contracts;

public interface ICategoryService
{
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete. Alt kategorisi varsa <see cref="InvalidOperationException"/> firlatir
    /// (Karar B: children-cascade kapali; once cocuklari tasinmali/silinmeli).
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>UI indent gosterimi icin pre-walk + Depth hesabi.</summary>
    Task<IReadOnlyList<CategoryTreeNodeDto>> ListWithIndentAsync(CancellationToken cancellationToken = default);
}
