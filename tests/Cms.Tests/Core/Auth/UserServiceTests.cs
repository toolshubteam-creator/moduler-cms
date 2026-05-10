namespace Cms.Tests.Core.Auth;

using Cms.Core.Auth;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection(MySqlCollection.Name)]
public class UserServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;
    private readonly Pbkdf2PasswordHasher _hasher = new();

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("userservice");
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_connStr, ServerVersion.AutoDetect(_connStr))
            .Options;
        return new MasterDbContext(options);
    }

    private async Task SeedUserAsync(string email, string password, bool isActive = true)
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        var user = new User
        {
            Email = email.ToLowerInvariant(),
            DisplayName = "Test",
            PasswordHash = _hasher.Hash(password),
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Authenticate_ValidCredentials_ReturnsSuccess()
    {
        await SeedUserAsync("alice@example.com", "Sakarya123!");
        await using var ctx = CreateContext();
        var sut = new UserService(ctx, _hasher);

        var result = await sut.AuthenticateAsync("alice@example.com", "Sakarya123!");

        result.IsSuccess.Should().BeTrue();
        result.User!.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task Authenticate_WrongPassword_ReturnsWrongPassword()
    {
        await SeedUserAsync("bob@example.com", "Sakarya123!");
        await using var ctx = CreateContext();
        var sut = new UserService(ctx, _hasher);

        var result = await sut.AuthenticateAsync("bob@example.com", "BogusPassword!");

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AuthenticationOutcome.WrongPassword);
    }

    [Fact]
    public async Task Authenticate_InactiveUser_ReturnsInactive()
    {
        await SeedUserAsync("carol@example.com", "Sakarya123!", isActive: false);
        await using var ctx = CreateContext();
        var sut = new UserService(ctx, _hasher);

        var result = await sut.AuthenticateAsync("carol@example.com", "Sakarya123!");

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AuthenticationOutcome.InactiveUser);
    }

    [Fact]
    public async Task Authenticate_UnknownEmail_ReturnsUserNotFound()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        var sut = new UserService(ctx, _hasher);

        var result = await sut.AuthenticateAsync("ghost@example.com", "any");

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AuthenticationOutcome.UserNotFound);
    }
}
