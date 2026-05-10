namespace Cms.Tests.Core.Tenancy;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MySql;
using Xunit;

public class SubdomainTenantResolverTests : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("cms_test")
        .WithUsername("test")
        .WithPassword("Test_Password_2026!")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_container.GetConnectionString(), ServerVersion.AutoDetect(_container.GetConnectionString()))
            .Options;
        return new MasterDbContext(options);
    }

    private async Task SeedTenantAsync(string slug, bool isActive = true)
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        ctx.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            DisplayName = slug,
            ConnectionString = "fake",
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private static HttpContext BuildContext(string host, string? queryString = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        if (queryString is not null)
        {
            ctx.Request.QueryString = new QueryString(queryString);
        }

        return ctx;
    }

    private static SubdomainTenantResolver CreateResolver(MasterDbContext db, bool allowQueryFallback = false, string rootDomain = "cms.local")
    {
        var opts = Options.Create(new TenancyOptions
        {
            RootDomain = rootDomain,
            AllowQueryFallback = allowQueryFallback,
        });
        return new SubdomainTenantResolver(db, opts);
    }

    [Fact]
    public async Task Resolve_ValidSubdomain_ReturnsTenant()
    {
        await SeedTenantAsync("acme");
        await using var db = CreateContext();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(BuildContext("acme.cms.local"));

        result.Should().NotBeNull();
        result!.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task Resolve_RootDomainOnly_ReturnsNull()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(BuildContext("cms.local"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_UnknownSubdomain_ReturnsNull()
    {
        await SeedTenantAsync("acme");
        await using var db = CreateContext();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(BuildContext("unknown.cms.local"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_InactiveTenant_ReturnsNull()
    {
        await SeedTenantAsync("acme", isActive: false);
        await using var db = CreateContext();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(BuildContext("acme.cms.local"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_QueryFallback_WhenEnabled_ReturnsTenant()
    {
        await SeedTenantAsync("acme");
        await using var db = CreateContext();
        var sut = CreateResolver(db, allowQueryFallback: true);

        var result = await sut.ResolveAsync(BuildContext("localhost", "?tenant=acme"));

        result.Should().NotBeNull();
        result!.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task Resolve_QueryFallback_WhenDisabled_ReturnsNull()
    {
        await SeedTenantAsync("acme");
        await using var db = CreateContext();
        var sut = CreateResolver(db, allowQueryFallback: false);

        var result = await sut.ResolveAsync(BuildContext("localhost", "?tenant=acme"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_NestedSubdomain_IsRejected()
    {
        await SeedTenantAsync("acme");
        await using var db = CreateContext();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(BuildContext("extra.acme.cms.local"));

        result.Should().BeNull();
    }
}
