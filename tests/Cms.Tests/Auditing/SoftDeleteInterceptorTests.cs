namespace Cms.Tests.Auditing;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Modules;
using Cms.Tests.Auditing.Fixtures;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

[Collection(MySqlCollection.Name)]
public class SoftDeleteInterceptorTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("softdel");

        var moduleAssembly = typeof(TestAuditModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new()
            {
                Instance = new TestAuditModule(),
                Assembly = moduleAssembly,
                DllPath = moduleAssembly.Location,
            },
        };

        _factory = new TenantDbContextFactory(modules, new IInterceptor[] { new SoftDeleteInterceptor() });

        await using var ctx = _factory.Create(_connStr);
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    [Fact]
    public async Task Remove_OnSoftDeletable_SetsIsDeletedTrueWithDeletedAtUtc()
    {
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestSoftDeletableEntity { Name = "y" };
            setup.Set<TestSoftDeletableEntity>().Add(e);
            await setup.SaveChangesAsync();
            id = e.Id;
        }

        var beforeUtc = DateTime.UtcNow.AddSeconds(-1);
        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestSoftDeletableEntity>().FirstAsync(e => e.Id == id);
            ctx.Set<TestSoftDeletableEntity>().Remove(loaded);
            await ctx.SaveChangesAsync();
        }
        var afterUtc = DateTime.UtcNow.AddSeconds(1);

        await using var query = _factory.Create(_connStr);
        var row = await query.Set<TestSoftDeletableEntity>()
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == id);
        row.IsDeleted.Should().BeTrue();
        row.DeletedAt.Should().NotBeNull();
        row.DeletedAt!.Value.Should().BeOnOrAfter(beforeUtc).And.BeOnOrBefore(afterUtc);
    }

    [Fact]
    public async Task DefaultQuery_DoesNotReturnSoftDeletedEntities()
    {
        Guid keepId;
        Guid deletedId;
        await using (var setup = _factory.Create(_connStr))
        {
            var keep = new TestSoftDeletableEntity { Name = "keep" };
            var del = new TestSoftDeletableEntity { Name = "del" };
            setup.Set<TestSoftDeletableEntity>().AddRange(keep, del);
            await setup.SaveChangesAsync();
            keepId = keep.Id;
            deletedId = del.Id;
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestSoftDeletableEntity>().FirstAsync(e => e.Id == deletedId);
            ctx.Set<TestSoftDeletableEntity>().Remove(loaded);
            await ctx.SaveChangesAsync();
        }

        await using var query = _factory.Create(_connStr);
        var allDefault = await query.Set<TestSoftDeletableEntity>().ToListAsync();
        allDefault.Should().ContainSingle(e => e.Id == keepId);
        allDefault.Should().NotContain(e => e.Id == deletedId);
    }

    [Fact]
    public async Task IgnoreQueryFilters_ReturnsSoftDeletedEntities()
    {
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestSoftDeletableEntity { Name = "ignore-me" };
            setup.Set<TestSoftDeletableEntity>().Add(e);
            await setup.SaveChangesAsync();
            id = e.Id;
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestSoftDeletableEntity>().FirstAsync(e => e.Id == id);
            ctx.Set<TestSoftDeletableEntity>().Remove(loaded);
            await ctx.SaveChangesAsync();
        }

        await using var query = _factory.Create(_connStr);
        var found = await query.Set<TestSoftDeletableEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id);
        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
    }
}
