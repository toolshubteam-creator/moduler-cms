namespace Cms.Tests.Modules.Seo;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Modules.Seo;
using Cms.Modules.Seo.Contracts;
using Cms.Modules.Seo.Domain;
using Cms.Modules.Seo.Services;
using Cms.Modules.Settings;
using Cms.Modules.Settings.Services;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class SeoMetaServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 200 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("seo");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var seoAsm = typeof(SeoModule).Assembly;
        var settingsAsm = typeof(SettingsModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new() { Instance = new SeoModule(), Assembly = seoAsm, DllPath = seoAsm.Location },
            new() { Instance = new SettingsModule(), Assembly = settingsAsm, DllPath = settingsAsm.Location },
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

    private SeoMetaService CreateService(TenantDbContext ctx) => new(ctx, new SettingsService(ctx));

    [Fact]
    public async Task SetAsync_NewTarget_InsertsRow_AndAuditsCreate()
    {
        SeoMeta saved;
        await using (var ctx = _factory.Create(_connStr))
        {
            saved = await CreateService(ctx).SetAsync("post", "42",
                new SeoMetaInput("Hello", "Lorem ipsum", null, null, "index, follow"));
        }

        saved.Title.Should().Be("Hello");
        saved.Description.Should().Be("Lorem ipsum");
        saved.Robots.Should().Be("index, follow");

        await using var verify = _factory.Create(_connStr);
        var row = await verify.Set<SeoMetaEntity>().FirstAsync(e => e.Id == saved.Id);
        row.TargetType.Should().Be("post");
        row.TargetId.Should().Be("42");

        var audit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(SeoMetaEntity)
                                   && a.Action == AuditAction.Create
                                   && a.EntityId == saved.Id.ToString());
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(200);
    }

    [Fact]
    public async Task SetAsync_ExistingTarget_Updates_AuditsUpdate_ChangesJson()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var first = await CreateService(ctx).SetAsync("post", "1",
                new SeoMetaInput("Old Title", "Old desc", null, null, null));
            id = first.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("post", "1",
                new SeoMetaInput("New Title", "Old desc", null, null, null));
        }

        await using var verify = _factory.Create(_connStr);
        var row = await verify.Set<SeoMetaEntity>().FirstAsync(e => e.Id == id);
        row.Title.Should().Be("New Title");

        var updates = await verify.Set<AuditEntry>()
            .Where(a => a.EntityName == nameof(SeoMetaEntity)
                     && a.Action == AuditAction.Update
                     && a.EntityId == id.ToString())
            .ToListAsync();
        updates.Should().HaveCount(1);
        updates[0].Changes.Should().NotBeNull();
        updates[0].Changes!.Should().Contain("\"title\"");
        updates[0].Changes!.Should().Contain("Old Title");
        updates[0].Changes!.Should().Contain("New Title");
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        await using var ctx = _factory.Create(_connStr);
        var result = await CreateService(ctx).GetAsync("nope", "x");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_HardDeletes_AuditsDelete()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var saved = await CreateService(ctx).SetAsync("page", "home",
                new SeoMetaInput("Home", null, null, null, null));
            id = saved.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            var ok = await CreateService(ctx).DeleteAsync("page", "home");
            ok.Should().BeTrue();
        }

        await using var verify = _factory.Create(_connStr);
        var exists = await verify.Set<SeoMetaEntity>().AnyAsync(e => e.Id == id);
        exists.Should().BeFalse();

        var deleteAudit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(SeoMetaEntity)
                                   && a.Action == AuditAction.Delete
                                   && a.EntityId == id.ToString());
        deleteAudit.Should().NotBeNull();
    }

    [Fact]
    public async Task SetAsync_DuplicateTargetWithoutUpdate_ThrowsOnDirectInsert()
    {
        // SetAsync upsert; direct duplicate insert (DbContext.Add) UNIQUE constraint vurmali
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("page", "unique", new SeoMetaInput("Once", null, null, null, null));
        }

        var act = async () =>
        {
            await using var ctx = _factory.Create(_connStr);
            ctx.Set<SeoMetaEntity>().Add(new SeoMetaEntity
            {
                TargetType = "page",
                TargetId = "unique",
                Title = "Duplicate",
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        };

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ResolveAsync_MetaMissing_FallsBackToSettingsDefaults()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await new SettingsService(ctx).SetAsync("seo.default_title", "Default Site Title");
            await new SettingsService(ctx).SetAsync("seo.default_description", "Default tagline");
        }

        await using var ctx2 = _factory.Create(_connStr);
        var resolved = await CreateService(ctx2).ResolveAsync("page", "missing");

        resolved.Title.Should().Be("Default Site Title");
        resolved.Description.Should().Be("Default tagline");
        resolved.OgImage.Should().BeNull();
        resolved.Canonical.Should().BeNull();
        resolved.Robots.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_MetaPartial_OverlaysSettingsDefaults()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await new SettingsService(ctx).SetAsync("seo.default_title", "Default T");
            await new SettingsService(ctx).SetAsync("seo.default_description", "Default D");
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("post", "5",
                new SeoMetaInput("Specific Title", null, null, null, null));
        }

        await using var ctx3 = _factory.Create(_connStr);
        var resolved = await CreateService(ctx3).ResolveAsync("post", "5");

        resolved.Title.Should().Be("Specific Title");
        resolved.Description.Should().Be("Default D");
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRows_OrderedByTargetTypeThenId()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            var svc = CreateService(ctx);
            await svc.SetAsync("post", "2", new SeoMetaInput("P2", null, null, null, null));
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("post", "1", new SeoMetaInput("P1", null, null, null, null));
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("category", "1", new SeoMetaInput("C1", null, null, null, null));
        }

        await using var read = _factory.Create(_connStr);
        var list = await CreateService(read).ListAsync();
        list.Should().HaveCount(3);
        list[0].TargetType.Should().Be("category");
        list[1].TargetType.Should().Be("post");
        list[1].TargetId.Should().Be("1");
        list[2].TargetId.Should().Be("2");
    }
}
