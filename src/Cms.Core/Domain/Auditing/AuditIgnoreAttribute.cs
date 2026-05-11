namespace Cms.Core.Domain.Auditing;

/// <summary>
/// Bu attribute ile isaretli property AuditSaveChangesInterceptor tarafindan
/// Changes JSON'una dahil edilmez. Sifre hash, kart no gibi hassas alanlar icin.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AuditIgnoreAttribute : Attribute;
