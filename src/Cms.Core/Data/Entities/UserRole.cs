namespace Cms.Core.Data.Entities;

public sealed class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AssignedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
