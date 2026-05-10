namespace Cms.Core.Authorization;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, Guid? tenantId, string permissionKey, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetUserPermissionsAsync(int userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
