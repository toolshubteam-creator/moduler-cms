namespace Cms.Core.Data.Entities;

public sealed class Permission
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ModuleId { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
