namespace Cms.Tests.Modules.Media;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Core.Tenancy;
using Cms.Modules.Media;
using Cms.Modules.Media.Contracts;
using Cms.Modules.Media.Domain;
using Cms.Modules.Media.Services;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

[Collection(MySqlCollection.Name)]
public class MediaServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 100 };
    private readonly FakeTenantContext _tenant = new();
    private readonly string _diskRoot = Path.Combine(Path.GetTempPath(), $"cms-media-{Guid.NewGuid():N}");
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;
    private LocalDiskFileStorage _storage = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_diskRoot);
        _connStr = await fixture.CreateDatabaseAsync("media");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var asm = typeof(MediaModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new()
            {
                Instance = new MediaModule(),
                Assembly = asm,
                DllPath = asm.Location,
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

        _tenant.Set(new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = "mediatest",
            DisplayName = "Media Test",
            ConnectionString = _connStr,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var opts = Options.Create(new MediaStorageOptions { StoragePath = _diskRoot });
        _storage = new LocalDiskFileStorage(opts, new FakeWebHostEnvironment { ContentRootPath = _diskRoot });
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_diskRoot, recursive: true); } catch { /* test cleanup */ }
        return fixture.DropDatabaseAsync(_connStr);
    }

    private MediaService CreateService(TenantDbContext ctx) => new(ctx, _storage, _tenant);

    private static MemoryStream Stream(string content) => new(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task UploadAsync_NewFile_InsertsRow_AndAuditsCreate()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var saved = await CreateService(ctx).UploadAsync(Stream("hello"), "hello.txt", "text/plain", "greeting");
            id = saved.Id;
        }

        await using var verify = _factory.Create(_connStr);
        var row = await verify.Set<MediaFileEntity>().FirstAsync(e => e.Id == id);
        row.FileName.Should().Be("hello.txt");
        row.MimeType.Should().Be("text/plain");
        row.SizeBytes.Should().Be(5);
        row.Hash.Should().HaveLength(64);
        row.AltText.Should().Be("greeting");

        var audit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(MediaFileEntity)
                                   && a.Action == AuditAction.Create
                                   && a.EntityId == id.ToString());
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(100);
    }

    [Fact]
    public async Task UploadAsync_SameContentTwice_TwoDbRows_OneDiskFile()
    {
        const string body = "dedup-payload";
        int id1, id2;
        await using (var ctx = _factory.Create(_connStr))
        {
            var a = await CreateService(ctx).UploadAsync(Stream(body), "a.txt", "text/plain", null);
            id1 = a.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            var b = await CreateService(ctx).UploadAsync(Stream(body), "b.txt", "text/plain", null);
            id2 = b.Id;
        }

        id1.Should().NotBe(id2);

        await using var verify = _factory.Create(_connStr);
        var rows = await verify.Set<MediaFileEntity>()
            .Where(e => e.Id == id1 || e.Id == id2)
            .OrderBy(e => e.Id)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Hash.Should().Be(rows[1].Hash);
        rows[0].StoredPath.Should().Be(rows[1].StoredPath);

        var dir = Path.GetDirectoryName(Path.Combine(_diskRoot, rows[0].StoredPath.Replace('/', Path.DirectorySeparatorChar)))!;
        Directory.GetFiles(dir).Should().HaveCount(1, "B yaklasimi: ayni hash icin disk'te tek dosya");
    }

    [Fact]
    public async Task UpdateMetadataAsync_AltTextChanged_AuditsUpdate()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var saved = await CreateService(ctx).UploadAsync(Stream("u"), "u.bin", "application/octet-stream", "before");
            id = saved.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).UpdateMetadataAsync(id, "after");
        }

        await using var verify = _factory.Create(_connStr);
        var entity = await verify.Set<MediaFileEntity>().FirstAsync(e => e.Id == id);
        entity.AltText.Should().Be("after");

        var updateAudit = await verify.Set<AuditEntry>()
            .Where(a => a.EntityName == nameof(MediaFileEntity)
                     && a.Action == AuditAction.Update
                     && a.EntityId == id.ToString())
            .ToListAsync();
        updateAudit.Should().HaveCount(1);
        updateAudit[0].Changes.Should().NotBeNull();
        updateAudit[0].Changes!.Should().Contain("\"altText\"");
        updateAudit[0].Changes!.Should().Contain("before");
        updateAudit[0].Changes!.Should().Contain("after");
    }

    [Fact]
    public async Task DeleteAsync_SetsIsDeleted_HidesFromDefaultQuery_AuditsDelete()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var saved = await CreateService(ctx).UploadAsync(Stream("d"), "d.bin", "application/octet-stream", null);
            id = saved.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            var ok = await CreateService(ctx).DeleteAsync(id);
            ok.Should().BeTrue();
        }

        await using var verify = _factory.Create(_connStr);

        var defaultQuery = await verify.Set<MediaFileEntity>().FirstOrDefaultAsync(e => e.Id == id);
        defaultQuery.Should().BeNull("soft-delete query filter ile gozukmemeli");

        var raw = await verify.Set<MediaFileEntity>().IgnoreQueryFilters().FirstAsync(e => e.Id == id);
        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();

        var deleteAudit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(MediaFileEntity)
                                   && a.Action == AuditAction.Delete
                                   && a.EntityId == id.ToString());
        deleteAudit.Should().NotBeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyNonDeletedRows_OrderByUploadedAtDesc()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).UploadAsync(Stream("first"), "a.txt", "text/plain", null);
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).UploadAsync(Stream("second"), "b.txt", "text/plain", null);
        }
        int thirdId;
        await using (var ctx = _factory.Create(_connStr))
        {
            var third = await CreateService(ctx).UploadAsync(Stream("third"), "c.txt", "text/plain", null);
            thirdId = third.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).DeleteAsync(thirdId);
        }

        await using var read = _factory.Create(_connStr);
        var list = await CreateService(read).ListAsync(skip: 0, take: 50);
        list.Should().HaveCount(2);
        list[0].FileName.Should().Be("b.txt", "ListAsync UploadedAt DESC sirali");
        list[1].FileName.Should().Be("a.txt");
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Tenant? Current { get; private set; }
        public bool IsResolved => Current is not null;
        public void Set(Tenant tenant) => Current = tenant;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Cms.Tests";
    }
}
