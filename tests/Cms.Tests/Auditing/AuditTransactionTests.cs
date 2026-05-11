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
public class AuditTransactionTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 99 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("audittx");

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
    public async Task ForcedAuditFail_RollsBackMainEntity()
    {
        // Audit_Entries tablosunu sil — interceptor'in ikinci SaveChanges'i (audit insert)
        // MySQL "table doesn't exist" hatasi atacak. D-017: bu durumda owned transaction
        // rollback olmali, main entity DB'de KALMAMALI.
        await using (var setup = _factory.Create(_connStr))
        {
            await setup.Database.ExecuteSqlRawAsync("DROP TABLE `Audit_Entries`;");
        }

        var entity = new TestAuditableEntity { Name = "should-not-persist" };

        await using var ctx = _factory.Create(_connStr);
        ctx.Set<TestAuditableEntity>().Add(entity);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        // Owned transaction rollback ile main entity de geri alindi.
        await using var verify = _factory.Create(_connStr);
        var stillThere = await verify.Set<TestAuditableEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == entity.Id);
        stillThere.Should().BeFalse("D-017 transaction wrapping main entity'i geri almaliydi");
    }

    [Fact]
    public async Task OuterTransactionExists_InterceptorDoesNotOwnTransaction()
    {
        // Caller transaction acmissa interceptor commit/rollback yapmamali.
        // Caller rollback edince hem main entity hem audit row birlikte geri alinmali
        // (audit insert ayni outer transaction icinde olmasi sart).
        var entityId = Guid.NewGuid();

        await using var ctx = _factory.Create(_connStr);
        await using var tx = await ctx.Database.BeginTransactionAsync();

        ctx.Set<TestAuditableEntity>().Add(new TestAuditableEntity { Id = entityId, Name = "outertx" });
        await ctx.SaveChangesAsync();

        // Caller rollback yapar — interceptor'in owned tx'i yoksa rollback iki tarafi da temizler.
        await tx.RollbackAsync();

        await using var verify = _factory.Create(_connStr);
        var mainExists = await verify.Set<TestAuditableEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == entityId);
        mainExists.Should().BeFalse("outer caller rollback'i main entity'i geri almali");

        var auditExists = await verify.Set<AuditEntry>()
            .AsNoTracking()
            .AnyAsync(a => a.EntityId == entityId.ToString());
        auditExists.Should().BeFalse("outer caller rollback'i audit row'u da geri almali (interceptor commit etmemis olmali)");
    }

    [Fact]
    public async Task SuccessfulSave_CommitsOwnedTransaction_AndPersistsBoth()
    {
        var entity = new TestAuditableEntity { Name = "happy-path" };

        await using (var ctx = _factory.Create(_connStr))
        {
            ctx.Set<TestAuditableEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.Create(_connStr);
        var mainExists = await verify.Set<TestAuditableEntity>().AnyAsync(e => e.Id == entity.Id);
        var auditExists = await verify.Set<AuditEntry>().AnyAsync(a => a.EntityId == entity.Id.ToString());
        mainExists.Should().BeTrue();
        auditExists.Should().BeTrue();
    }
}
