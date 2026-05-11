namespace Cms.Tests.Auditing;

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
public class AuditRestoreDetectionTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 5 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("auditrestore");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

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
            new AuditSaveChangesInterceptor(sp),
        };
        _factory = new TenantDbContextFactory(modules, interceptors);

        await using var ctx = _factory.Create(_connStr);
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    [Fact]
    public async Task SoftDeletable_IsDeletedTrueToFalse_LogsAsRestoreAction()
    {
        Guid id;
        await using (var setup = _factory.Create(_connStr))
        {
            var entity = new TestSoftDeletableEntity { Name = "to-restore" };
            setup.Set<TestSoftDeletableEntity>().Add(entity);
            await setup.SaveChangesAsync();
            id = entity.Id;
        }

        // Sil (SoftDeleteInterceptor -> IsDeleted=true -> AuditAction.Delete)
        await using (var del = _factory.Create(_connStr))
        {
            var entity = await del.Set<TestSoftDeletableEntity>().FirstAsync(e => e.Id == id);
            del.Set<TestSoftDeletableEntity>().Remove(entity);
            await del.SaveChangesAsync();
        }

        // Restore: IgnoreQueryFilters ile yukle, IsDeleted=false yap, kaydet -> AuditAction.Restore
        await using (var restore = _factory.Create(_connStr))
        {
            var entity = await restore.Set<TestSoftDeletableEntity>()
                .IgnoreQueryFilters()
                .FirstAsync(e => e.Id == id);
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            await restore.SaveChangesAsync();
        }

        await using var query = _factory.Create(_connStr);
        var actions = await query.Set<AuditEntry>()
            .Where(a => a.EntityId == id.ToString())
            .OrderBy(a => a.Timestamp)
            .Select(a => a.Action)
            .ToListAsync();

        actions.Should().BeEquivalentTo(
            new[] { AuditAction.Create, AuditAction.Delete, AuditAction.Restore },
            opts => opts.WithStrictOrdering());
    }
}
