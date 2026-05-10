namespace Cms.Tests.Core.Authorization;

using Cms.Core.Authorization;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection(MySqlCollection.Name)]
public class PermissionServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;

    public async Task InitializeAsync() => _connStr = await fixture.CreateDatabaseAsync("permsvc");

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_connStr, ServerVersion.AutoDetect(_connStr))
            .Options;
        return new MasterDbContext(options);
    }

    private async Task<(int userId, int roleId)> SeedUserAndRoleAsync(bool isSystemRole = false)
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        var user = new User { Email = "u@example.com", PasswordHash = "h", DisplayName = "U", CreatedAtUtc = DateTime.UtcNow };
        var role = new Role { Name = isSystemRole ? "Admin" : "Editor", IsSystem = isSystemRole, CreatedAtUtc = DateTime.UtcNow };
        ctx.Users.Add(user);
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();
        return (user.Id, role.Id);
    }

    private async Task AssignRoleAsync(int userId, int roleId, Guid? tenantId)
    {
        await using var ctx = CreateContext();
        ctx.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId, TenantId = tenantId, AssignedAtUtc = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    private async Task<int> CreatePermissionAsync(string key, int? assignToRoleId = null)
    {
        await using var ctx = CreateContext();
        var perm = new Permission { Key = key, DisplayName = key, ModuleId = "test" };
        ctx.Permissions.Add(perm);
        await ctx.SaveChangesAsync();
        if (assignToRoleId.HasValue)
        {
            ctx.RolePermissions.Add(new RolePermission { RoleId = assignToRoleId.Value, PermissionId = perm.Id });
            await ctx.SaveChangesAsync();
        }

        return perm.Id;
    }

    [Fact]
    public async Task HasPermission_SystemRole_BypassesCheck()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync(isSystemRole: true);
        await AssignRoleAsync(userId, roleId, tenantId: null);
        await using var ctx = CreateContext();
        var sut = new PermissionService(ctx);

        var result = await sut.HasPermissionAsync(userId, null, "anything.really");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermission_RoleHasPermission_ReturnsTrue()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        await AssignRoleAsync(userId, roleId, tenantId: null);
        await CreatePermissionAsync("blog.posts.create", assignToRoleId: roleId);
        await using var ctx = CreateContext();
        var sut = new PermissionService(ctx);

        var result = await sut.HasPermissionAsync(userId, null, "blog.posts.create");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermission_RoleLacksPermission_ReturnsFalse()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        await AssignRoleAsync(userId, roleId, tenantId: null);
        await CreatePermissionAsync("blog.posts.create");
        await using var ctx = CreateContext();
        var sut = new PermissionService(ctx);

        var result = await sut.HasPermissionAsync(userId, null, "blog.posts.create");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_UserHasNoRoles_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        var user = new User { Email = "lonely@example.com", PasswordHash = "h", DisplayName = "U", CreatedAtUtc = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = new PermissionService(ctx);

        var result = await sut.HasPermissionAsync(user.Id, null, "any.perm");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_TenantSpecificRole_OnlyAppliesToThatTenant()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await AssignRoleAsync(userId, roleId, tenantId: tenantA);
        await CreatePermissionAsync("blog.posts.create", assignToRoleId: roleId);
        await using var ctx = CreateContext();
        var sut = new PermissionService(ctx);

        var inTenantA = await sut.HasPermissionAsync(userId, tenantA, "blog.posts.create");
        var inTenantB = await sut.HasPermissionAsync(userId, tenantB, "blog.posts.create");

        inTenantA.Should().BeTrue();
        inTenantB.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserPermissions_SystemRole_ReturnsAllPermissions()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync(isSystemRole: true);
        await AssignRoleAsync(userId, roleId, tenantId: null);
        await CreatePermissionAsync("blog.posts.create");
        await CreatePermissionAsync("blog.posts.delete");
        await using var ctx = CreateContext();
        var sut = new PermissionService(ctx);

        var result = await sut.GetUserPermissionsAsync(userId, null);

        result.Should().Contain(["blog.posts.create", "blog.posts.delete"]);
    }
}
