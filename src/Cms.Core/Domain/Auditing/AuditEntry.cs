namespace Cms.Core.Domain.Auditing;

/// <summary>
/// Tenant DB'sindeki Audit_Entries tablosunu temsil eder. Her IAuditable entity'nin
/// her degisikligi (Create/Update/Delete/Restore) icin bir satir uretilir.
/// Bu entity'nin kendisi IAuditable veya ISoftDeletable DEGILDIR (recursive audit olmasin).
/// </summary>
public sealed class AuditEntry
{
    public int Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    public int? UserId { get; set; }

    public DateTime Timestamp { get; set; }

    public string? Changes { get; set; }
}
