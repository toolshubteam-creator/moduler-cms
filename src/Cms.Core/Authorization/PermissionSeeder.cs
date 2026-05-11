namespace Cms.Core.Authorization;

using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Modules;
using Cms.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PermissionSeeder(
    MasterDbContext db,
    IReadOnlyList<ModuleDescriptor> modules,
    IPermissionCacheInvalidator cacheInvalidator,
    ILogger<PermissionSeeder> logger)
{
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var declared = new List<(PermissionDescriptor Desc, string ModuleId)>();

        // Cekirdek platform permission'lari ("core" reserved module id).
        foreach (var desc in CorePermissions.All)
        {
            var normalized = desc.Key.Trim().ToLowerInvariant();
            if (!normalized.StartsWith(CorePermissions.ModuleId + ".", StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Core permission key '{Key}' '{Prefix}.' ile baslamiyor, atlandi.",
                    desc.Key, CorePermissions.ModuleId);
                continue;
            }
            declared.Add((desc with { Key = normalized }, CorePermissions.ModuleId));
        }

        // Modul permission'lari — module id == "core" rezerve oldugu icin bu yola dusmez.
        foreach (var module in modules)
        {
            if (string.Equals(module.Manifest.Id, CorePermissions.ModuleId, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Modul '{Module}' rezerve 'core' id'sini kullaniyor, permission seed'i atlandi.",
                    module.Manifest.Id);
                continue;
            }

            if (module.Instance is not IHasPermissions hasPermissions)
            {
                continue;
            }

            foreach (var desc in hasPermissions.GetPermissions())
            {
                if (string.IsNullOrWhiteSpace(desc.Key))
                {
                    logger.LogWarning("Modul '{Module}' bos permission key icin atlandi.", module.Manifest.Id);
                    continue;
                }

                var normalized = desc.Key.Trim().ToLowerInvariant();
                if (!normalized.StartsWith(module.Manifest.Id + ".", StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "Modul '{Module}' permission key '{Key}' modul prefix'i ile baslamiyor, atlandi.",
                        module.Manifest.Id, desc.Key);
                    continue;
                }

                declared.Add((desc with { Key = normalized }, module.Manifest.Id));
            }
        }

        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, cancellationToken);

        foreach (var (desc, moduleId) in declared)
        {
            if (existing.TryGetValue(desc.Key, out var found))
            {
                if (found.DisplayName != desc.DisplayName ||
                    found.Description != desc.Description ||
                    found.ModuleId != moduleId)
                {
                    found.DisplayName = desc.DisplayName;
                    found.Description = desc.Description;
                    found.ModuleId = moduleId;
                }
            }
            else
            {
                db.Permissions.Add(new Permission
                {
                    Key = desc.Key,
                    DisplayName = desc.DisplayName,
                    Description = desc.Description,
                    ModuleId = moduleId,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Seed sonrasi cache stale olmasin — yeni eklenen/guncellenen permission'lar
        // bekleyen istegin set'inde olmayabilir.
        cacheInvalidator.InvalidateAll();
    }
}
