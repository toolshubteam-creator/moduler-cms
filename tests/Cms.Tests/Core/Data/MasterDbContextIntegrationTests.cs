namespace Cms.Tests.Core.Data;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection(MySqlCollection.Name)]
public class MasterDbContextIntegrationTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("master");
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_connStr, ServerVersion.AutoDetect(_connStr))
            .Options;
        return new MasterDbContext(options);
    }

    [Fact]
    public async Task User_CanBeCreatedAndRetrieved()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "fake.hash.value",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var fromDb = await ctx.Users.FirstAsync(u => u.Email == "test@example.com");
        fromDb.Id.Should().BeGreaterThan(0);
        fromDb.DisplayName.Should().Be("Test User");
        fromDb.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task User_DuplicateEmail_ThrowsOnSaveChanges()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        ctx.Users.Add(new User { Email = "dup@example.com", PasswordHash = "h", DisplayName = "A", CreatedAtUtc = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        ctx.Users.Add(new User { Email = "dup@example.com", PasswordHash = "h", DisplayName = "B", CreatedAtUtc = DateTime.UtcNow });
        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UserRole_CanBeAssigned_WithTenantId()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var user = new User { Email = "u@example.com", PasswordHash = "h", DisplayName = "User", CreatedAtUtc = DateTime.UtcNow };
        var role = new Role { Name = "Editor", IsSystem = false, CreatedAtUtc = DateTime.UtcNow };
        ctx.Users.Add(user);
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();

        var tenantId = Guid.NewGuid();
        ctx.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, TenantId = tenantId, AssignedAtUtc = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var assignment = await ctx.UserRoles.FirstAsync(ur => ur.UserId == user.Id);
        assignment.TenantId.Should().Be(tenantId);
    }
}
