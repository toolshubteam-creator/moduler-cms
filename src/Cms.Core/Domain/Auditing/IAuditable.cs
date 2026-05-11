namespace Cms.Core.Domain.Auditing;

/// <summary>
/// Marker interface. Bir entity bu arayuzu implement ettiginde
/// AuditSaveChangesInterceptor onun her Create/Update/Delete operasyonu icin
/// Audit_Entries tablosuna bir kayit yazar.
/// </summary>
public interface IAuditable;
