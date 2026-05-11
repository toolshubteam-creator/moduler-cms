namespace Cms.Tests.Core.Authorization;

using System.Reflection;
using Cms.Abstractions.Modules;
using Cms.Abstractions.Modules.Permissions;
using Cms.Core.Authorization;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Modules;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using Xunit;

[Collection(MySqlCollection.Name)]
public class PermissionSeederTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;

    public async Task InitializeAsync() => _connStr = await fixture.CreateDatabaseAsync("permseed");

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_connStr, ServerVersion.AutoDetect(_connStr))
            .Options;
        return new MasterDbContext(options);
    }

    private static ModuleDescriptor BuildModule(string id, params PermissionDescriptor[] perms)
    {
        var manifest = new ModuleManifest
        {
            Id = id,
            Name = id,
            Version = NuGetVersion.Parse("1.0.0"),
            MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        };
        return new ModuleDescriptor
        {
            Instance = new StubModule(manifest, perms),
            Assembly = Assembly.GetExecutingAssembly(),
            DllPath = $"{id}.dll",
        };
    }

    private sealed class StubModule(ModuleManifest manifest, IReadOnlyList<PermissionDescriptor> perms) : ModuleBase
    {
        public override ModuleManifest Manifest { get; } = manifest;

        public override IReadOnlyList<PermissionDescriptor> GetPermissions() => perms;
    }

    [Fact]
    public async Task Reconcile_FreshDb_AddsPermissions()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        var modules = new[]
        {
            BuildModule(
                "blog",
                new PermissionDescriptor { Key = "blog.posts.create", DisplayName = "Yazi Olustur" },
                new PermissionDescriptor { Key = "blog.posts.delete", DisplayName = "Yazi Sil" }),
        };

        await using (var ctx = CreateContext())
        {
            var seeder = new PermissionSeeder(ctx, modules, new NoopInvalidator(), NullLogger<PermissionSeeder>.Instance);
            await seeder.ReconcileAsync();
        }

        await using var assert = CreateContext();
        var saved = await assert.Permissions.OrderBy(p => p.Key).ToListAsync();
        // Faz-3.3: 2 modul perm + 2 core perm (audit.view + softdelete.manage)
        var corePerms = saved.Where(p => p.ModuleId == "core").ToList();
        var blogPerms = saved.Where(p => p.ModuleId == "blog").OrderBy(p => p.Key).ToList();
        blogPerms.Should().HaveCount(2);
        blogPerms[0].Key.Should().Be("blog.posts.create");
        blogPerms[0].DisplayName.Should().Be("Yazi Olustur");
        corePerms.Should().Contain(p => p.Key == "core.audit.view");
        corePerms.Should().Contain(p => p.Key == "core.softdelete.manage");
    }

    [Fact]
    public async Task Reconcile_RunTwice_IsIdempotent()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        var modules = new[]
        {
            BuildModule("blog", new PermissionDescriptor { Key = "blog.posts.create", DisplayName = "X" }),
        };

        await using (var ctx1 = CreateContext())
        {
            await new PermissionSeeder(ctx1, modules, new NoopInvalidator(), NullLogger<PermissionSeeder>.Instance).ReconcileAsync();
        }

        await using (var ctx2 = CreateContext())
        {
            await new PermissionSeeder(ctx2, modules, new NoopInvalidator(), NullLogger<PermissionSeeder>.Instance).ReconcileAsync();
        }

        await using var assert = CreateContext();
        // Faz-3.3: 1 modul perm + 2 core perm; iki run sonrasi hala 3 olmali (idempotent).
        var count = await assert.Permissions.CountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task Reconcile_OrphanPermission_IsNotDeleted()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
            setup.Permissions.Add(new Permission
            {
                Key = "blog.posts.publish",
                DisplayName = "Eski Permission",
                ModuleId = "blog",
            });
            await setup.SaveChangesAsync();
        }

        var modules = new[]
        {
            BuildModule("blog", new PermissionDescriptor { Key = "blog.posts.create", DisplayName = "Y" }),
        };

        await using (var ctx = CreateContext())
        {
            await new PermissionSeeder(ctx, modules, new NoopInvalidator(), NullLogger<PermissionSeeder>.Instance).ReconcileAsync();
        }

        await using var assert = CreateContext();
        var orphan = await assert.Permissions.AnyAsync(p => p.Key == "blog.posts.publish");
        orphan.Should().BeTrue("orphan permission'lar kullanici atamalari kaybolmasin diye silinmemeli");
    }

    [Fact]
    public async Task Reconcile_PermissionKeyWithoutModulePrefix_IsSkipped()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        var modules = new[]
        {
            BuildModule(
                "blog",
                new PermissionDescriptor { Key = "wrong.prefix.key", DisplayName = "Skip" },
                new PermissionDescriptor { Key = "blog.posts.create", DisplayName = "OK" }),
        };

        await using (var ctx = CreateContext())
        {
            await new PermissionSeeder(ctx, modules, new NoopInvalidator(), NullLogger<PermissionSeeder>.Instance).ReconcileAsync();
        }

        await using var assert = CreateContext();
        var saved = await assert.Permissions.ToListAsync();
        // Faz-3.3: prefix-uyumsuz key atlanir; blog.posts.create + 2 core perm kalir.
        saved.Should().HaveCount(3);
        saved.Should().Contain(p => p.Key == "blog.posts.create");
        saved.Should().Contain(p => p.Key == "core.audit.view");
        saved.Should().Contain(p => p.Key == "core.softdelete.manage");
    }

    [Fact]
    public async Task Reconcile_OnCompletion_CallsInvalidateAll()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        var modules = new[]
        {
            BuildModule("blog", new PermissionDescriptor { Key = "blog.posts.create", DisplayName = "X" }),
        };

        var invalidator = new RecordingInvalidator();
        await using (var ctx = CreateContext())
        {
            await new PermissionSeeder(ctx, modules, invalidator, NullLogger<PermissionSeeder>.Instance).ReconcileAsync();
        }

        invalidator.InvalidateAllCallCount.Should().Be(1);
    }

    private sealed class NoopInvalidator : IPermissionCacheInvalidator
    {
        public void InvalidateUser(int userId) { }
        public Task InvalidateRoleAsync(int roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateAll() { }
    }

    private sealed class RecordingInvalidator : IPermissionCacheInvalidator
    {
        public int InvalidateAllCallCount { get; private set; }
        public void InvalidateUser(int userId) { }
        public Task InvalidateRoleAsync(int roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateAll() => InvalidateAllCallCount++;
    }
}
