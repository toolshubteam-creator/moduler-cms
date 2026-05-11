namespace Cms.Web.Areas.Admin.ViewModels;

using Cms.Core.Domain.Auditing;

public sealed class AuditFilterViewModel
{
    public string? EntityName { get; set; }

    public int? UserId { get; set; }

    public AuditAction? Action { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
