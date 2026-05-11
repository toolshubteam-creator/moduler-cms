namespace Cms.Web.Areas.Admin.ViewModels;

using Cms.Core.Domain.Auditing;

public sealed class AuditIndexViewModel
{
    public IReadOnlyList<TenantOption> Tenants { get; set; } = [];

    public Guid? SelectedTenantId { get; set; }

    public AuditFilterViewModel Filter { get; set; } = new();

    public IReadOnlyList<AuditEntry> Entries { get; set; } = [];

    public int Page { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed record TenantOption(Guid Id, string Slug, string DisplayName);
