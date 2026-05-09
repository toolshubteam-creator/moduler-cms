namespace Cms.Abstractions.Modules;

using Cms.Abstractions.Modules.Permissions;

public interface IHasPermissions
{
    IReadOnlyList<PermissionDescriptor> GetPermissions();
}
