namespace Cms.Core.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class HasPermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
