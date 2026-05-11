namespace Cms.Core.Security;

using Cms.Abstractions.Modules.Permissions;

/// <summary>
/// Cekirdek (modul-bagimsiz) platform permission'lari. PermissionSeeder bu listeyi
/// modul permission'larina ek olarak DB'ye yazar. "core" module id rezerve — hicbir
/// modul "core" id ile yuklenemez (modul prefix validation'i bu davranisi destekler).
/// </summary>
public static class CorePermissions
{
    public const string ModuleId = "core";

    public static readonly PermissionDescriptor AuditView = new()
    {
        Key = "core.audit.view",
        DisplayName = "Audit Log Goruntule",
        Description = "Tenant denetim kayitlarini goruntuleme yetkisi",
    };

    public static IReadOnlyList<PermissionDescriptor> All { get; } = new[] { AuditView };
}
