namespace Cms.Web.Areas.Admin.ViewModels;

public sealed class SoftDeleteIndexViewModel
{
    public IReadOnlyList<TenantOption> Tenants { get; set; } = [];

    public Guid? SelectedTenantId { get; set; }

    public IReadOnlyList<string> EntityTypeOptions { get; set; } = [];

    public string? SelectedEntityName { get; set; }

    public IReadOnlyList<DeletedEntityRow> Entries { get; set; } = [];

    public int Page { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed record DeletedEntityRow(string EntityId, string Display, DateTime? DeletedAt);
