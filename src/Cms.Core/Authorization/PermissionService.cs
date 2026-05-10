namespace Cms.Core.Authorization;

using Cms.Core.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PermissionService(MasterDbContext db) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(
        int userId,
        Guid? tenantId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        var hasSystemRole = await db.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId
                         && ur.Role.IsSystem
                         && (ur.TenantId == tenantId || ur.TenantId == null),
                cancellationToken);

        if (hasSystemRole)
        {
            return true;
        }

        var normalized = permissionKey.Trim().ToLowerInvariant();

        return await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId
                      && (ur.TenantId == tenantId || ur.TenantId == null))
            .SelectMany(ur => ur.Role.RolePermissions)
            .AnyAsync(rp => rp.Permission.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        int userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var hasSystemRole = await db.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId
                         && ur.Role.IsSystem
                         && (ur.TenantId == tenantId || ur.TenantId == null),
                cancellationToken);

        if (hasSystemRole)
        {
            var all = await db.Permissions.AsNoTracking().Select(p => p.Key).ToListAsync(cancellationToken);
            return new HashSet<string>(all, StringComparer.Ordinal);
        }

        var keys = await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId
                      && (ur.TenantId == tenantId || ur.TenantId == null))
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(keys, StringComparer.Ordinal);
    }
}
