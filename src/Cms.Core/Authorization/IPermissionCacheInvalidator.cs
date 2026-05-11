namespace Cms.Core.Authorization;

public interface IPermissionCacheInvalidator
{
    /// <summary>Bir kullanicinin tum tenant scope'lari icin cache entry'lerini siler.</summary>
    void InvalidateUser(int userId);

    /// <summary>Bir role'e atanmis butun kullanicilarin cache entry'lerini siler (DB query Sys_UserRoles).</summary>
    Task InvalidateRoleAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>Tum permission cache'ini bosaltir (seed/migration sonrasi cagrilir).</summary>
    void InvalidateAll();
}
