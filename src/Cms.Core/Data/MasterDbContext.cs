namespace Cms.Core.Data;

using Cms.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Sys_Users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Sys_Roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(128).IsRequired();
            b.Property(x => x.Description).HasMaxLength(512);
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("Sys_UserRoles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.HasIndex(x => new { x.UserId, x.RoleId, x.TenantId }).IsUnique();
            b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("Sys_Permissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).HasMaxLength(256).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            b.Property(x => x.ModuleId).HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(512);
            b.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("Sys_RolePermissions");
            b.HasKey(x => new { x.RoleId, x.PermissionId });
            b.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Sys_Tenants");
            b.HasKey(x => x.Id);
            b.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            b.Property(x => x.ConnectionString).HasMaxLength(1024).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
        });
    }
}
