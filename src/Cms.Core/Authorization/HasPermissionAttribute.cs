namespace Cms.Core.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        PermissionKey = permissionKey.Trim().ToLowerInvariant();
        Policy = PolicyPrefix + PermissionKey;
    }

    public string PermissionKey { get; }
}
