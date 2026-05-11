namespace Cms.Tests.Auditing;

using System.Reflection;
using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Tests.Auditing.Fixtures;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class AuditInterceptorTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new();
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("audit");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var rootProvider = services.BuildServiceProvider();

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

        var interceptors = new IInterceptor[]
        {
            new SoftDeleteInterceptor(),
            new AuditSaveChangesInterceptor(rootProvider),
        };

        _factory = new TenantDbContextFactory(modules, interceptors);

        await using var ctx = _factory.Create(_connStr);
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    [Fact]
    public async Task Create_WritesAuditEntry_ActionCreate_NoChanges()
    {
        _user.UserId = 42;
        var entity = new TestAuditableEntity { Name = "hello" };

        await using (var ctx = _factory.Create(_connStr))
        {
            ctx.Set<TestAuditableEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using var queryCtx = _factory.Create(_connStr);
        var audits = await queryCtx.Set<AuditEntry>().ToListAsync();
        audits.Should().HaveCount(1);
        audits[0].Action.Should().Be(AuditAction.Create);
        audits[0].UserId.Should().Be(42);
        audits[0].EntityName.Should().Be(nameof(TestAuditableEntity));
        audits[0].EntityId.Should().Be(entity.Id.ToString());
        audits[0].Changes.Should().BeNull();
    }

    [Fact]
    public async Task Update_WritesAuditEntry_ActionUpdate_ChangesContainsOldAndNew()
    {
        _user.UserId = 7;
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestAuditableEntity { Name = "original" };
            setup.Set<TestAuditableEntity>().Add(e);
            await setup.SaveChangesAsync();
            id = e.Id;
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestAuditableEntity>().FirstAsync(e => e.Id == id);
            loaded.Name = "updated";
            await ctx.SaveChangesAsync();
        }

        await using var queryCtx = _factory.Create(_connStr);
        var updates = await queryCtx.Set<AuditEntry>()
            .Where(a => a.Action == AuditAction.Update)
            .ToListAsync();
        updates.Should().HaveCount(1);
        updates[0].Changes.Should().NotBeNull();
        updates[0].Changes!.Should().Contain("\"name\"");
        updates[0].Changes!.Should().Contain("\"original\"");
        updates[0].Changes!.Should().Contain("\"updated\"");
    }

    [Fact]
    public async Task Update_WithAuditIgnoreField_OmitsIgnoredFieldFromChanges()
    {
        _user.UserId = 1;
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestAuditableEntity { Name = "n1", SecretField = "secret1" };
            setup.Set<TestAuditableEntity>().Add(e);
            await setup.SaveChangesAsync();
            id = e.Id;
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestAuditableEntity>().FirstAsync(e => e.Id == id);
            loaded.Name = "n2";
            loaded.SecretField = "secret2";
            await ctx.SaveChangesAsync();
        }

        await using var queryCtx = _factory.Create(_connStr);
        var update = await queryCtx.Set<AuditEntry>()
            .FirstAsync(a => a.Action == AuditAction.Update);
        update.Changes.Should().NotBeNull();
        update.Changes!.Should().Contain("\"name\"");
        update.Changes!.Should().NotContain("secret");
        update.Changes!.Should().NotContain("SecretField");
    }

    [Fact]
    public async Task Delete_SoftDeletable_WritesDeleteAudit_KeepsRow()
    {
        _user.UserId = 1;
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestSoftDeletableEntity { Name = "x" };
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

        await using var queryCtx = _factory.Create(_connStr);

        var deleteAudit = await queryCtx.Set<AuditEntry>()
            .FirstAsync(a => a.Action == AuditAction.Delete);
        deleteAudit.Changes.Should().BeNull();
        deleteAudit.EntityId.Should().Be(id.ToString());
        deleteAudit.EntityName.Should().Be(nameof(TestSoftDeletableEntity));

        var hardRow = await queryCtx.Set<TestSoftDeletableEntity>()
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == id);
        hardRow.IsDeleted.Should().BeTrue();
        hardRow.DeletedAt.Should().NotBeNull();

        var filtered = await queryCtx.Set<TestSoftDeletableEntity>()
            .FirstOrDefaultAsync(e => e.Id == id);
        filtered.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonSoftDeletable_HardDeletes_WritesDeleteAudit()
    {
        _user.UserId = 1;
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var e = new TestAuditableEntity { Name = "h" };
            setup.Set<TestAuditableEntity>().Add(e);
            await setup.SaveChangesAsync();
            id = e.Id;
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var loaded = await ctx.Set<TestAuditableEntity>().FirstAsync(e => e.Id == id);
            ctx.Set<TestAuditableEntity>().Remove(loaded);
            await ctx.SaveChangesAsync();
        }

        await using var queryCtx = _factory.Create(_connStr);
        var deleteAudit = await queryCtx.Set<AuditEntry>()
            .FirstAsync(a => a.Action == AuditAction.Delete);
        deleteAudit.EntityId.Should().Be(id.ToString());

        var row = await queryCtx.Set<TestAuditableEntity>().FirstOrDefaultAsync(e => e.Id == id);
        row.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithNullUser_WritesAuditWithNullUserId()
    {
        _user.UserId = null;
        await using (var ctx = _factory.Create(_connStr))
        {
            ctx.Set<TestAuditableEntity>().Add(new TestAuditableEntity { Name = "anon" });
            await ctx.SaveChangesAsync();
        }

        await using var queryCtx = _factory.Create(_connStr);
        var audit = await queryCtx.Set<AuditEntry>().FirstAsync();
        audit.UserId.Should().BeNull();
    }
}
