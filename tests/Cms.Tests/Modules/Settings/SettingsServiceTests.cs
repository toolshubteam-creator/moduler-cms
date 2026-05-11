namespace Cms.Tests.Modules.Settings;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Modules.Settings;
using Cms.Modules.Settings.Contracts;
using Cms.Modules.Settings.Domain;
using Cms.Modules.Settings.Services;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class SettingsServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 42 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("settings");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var settingsAsm = typeof(SettingsModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new()
            {
                Instance = new SettingsModule(),
                Assembly = settingsAsm,
                DllPath = settingsAsm.Location,
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

    private SettingsService CreateService(TenantDbContext ctx) => new(ctx);

    [Fact]
    public async Task SetAsync_NewStringKey_InsertsAndAuditsCreate()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("site.title", "Hello CMS");
        }

        await using var verify = _factory.Create(_connStr);
        var row = await verify.Set<SettingEntryEntity>().FirstAsync(e => e.Key == "site.title");
        row.Value.Should().Be("Hello CMS");
        row.ValueType.Should().Be(SettingValueType.String);

        var audit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(SettingEntryEntity)
                                   && a.Action == AuditAction.Create
                                   && a.EntityId == row.Id.ToString());
        audit.Should().NotBeNull("Create audit yazilmali");
        audit!.UserId.Should().Be(42);
    }

    [Fact]
    public async Task SetAsync_ExistingKey_UpdatesAndAuditsUpdate()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("count", 1);
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("count", 42);
        }

        await using var verify = _factory.Create(_connStr);
        var row = await verify.Set<SettingEntryEntity>().FirstAsync(e => e.Key == "count");
        row.Value.Should().Be("42");
        row.ValueType.Should().Be(SettingValueType.Int);

        var updates = await verify.Set<AuditEntry>()
            .Where(a => a.EntityName == nameof(SettingEntryEntity)
                     && a.Action == AuditAction.Update
                     && a.EntityId == row.Id.ToString())
            .ToListAsync();
        updates.Should().HaveCount(1);
        updates[0].Changes.Should().NotBeNull();
        updates[0].Changes!.Should().Contain("\"value\"");
    }

    [Fact]
    public async Task GetAsync_IntFromIntStorage_ReturnsParsedInt()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("items.max", 250);
        }

        await using var read = _factory.Create(_connStr);
        var value = await CreateService(read).GetAsync<int>("items.max");
        value.Should().Be(250);
    }

    [Fact]
    public async Task GetAsync_BoolFromBoolStorage_ReturnsParsedBool()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("feature.enabled", true);
        }

        await using var read = _factory.Create(_connStr);
        var value = await CreateService(read).GetAsync<bool>("feature.enabled");
        value.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsDefault()
    {
        await using var ctx = _factory.Create(_connStr);
        var value = await CreateService(ctx).GetAsync<int?>("no.such.key");
        value.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_TypeMismatch_ThrowsInvalidCastException()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("greeting", "merhaba");
        }

        await using var read = _factory.Create(_connStr);
        var act = async () => await CreateService(read).GetAsync<int>("greeting");
        await act.Should().ThrowAsync<InvalidCastException>();
    }

    [Fact]
    public async Task GetAsync_JsonPayload_DeserializesToType()
    {
        var payload = new TestDto { Name = "abc", Count = 5 };
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("nested", payload);
        }

        await using var read = _factory.Create(_connStr);
        var loaded = await CreateService(read).GetAsync<TestDto>("nested");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("abc");
        loaded.Count.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesAndAuditsDelete()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("temp", "x");
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            id = await ctx.Set<SettingEntryEntity>().Where(e => e.Key == "temp").Select(e => e.Id).FirstAsync();
            var ok = await CreateService(ctx).DeleteAsync("temp");
            ok.Should().BeTrue();
        }

        await using var verify = _factory.Create(_connStr);
        var stillThere = await verify.Set<SettingEntryEntity>().AnyAsync(e => e.Key == "temp");
        stillThere.Should().BeFalse();

        var deleteAudit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(SettingEntryEntity)
                                   && a.Action == AuditAction.Delete
                                   && a.EntityId == id.ToString());
        deleteAudit.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_ReturnsFalse()
    {
        await using var ctx = _factory.Create(_connStr);
        var ok = await CreateService(ctx).DeleteAsync("nope");
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllKeys()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            var sut = CreateService(ctx);
            await sut.SetAsync("k1", "v1");
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).SetAsync("k2", 7);
        }

        await using var read = _factory.Create(_connStr);
        var all = await CreateService(read).GetAllAsync();
        all.Should().HaveCount(2);
        all.Should().Contain(e => e.Key == "k1" && e.ValueType == SettingValueType.String);
        all.Should().Contain(e => e.Key == "k2" && e.ValueType == SettingValueType.Int);
    }

    private sealed class TestDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
