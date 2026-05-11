namespace Cms.Tests.Core.Authorization;

using Cms.Core.Authorization;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Collection(MySqlCollection.Name)]
public class MemoryPermissionCacheInvalidatorTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;

    public async Task InitializeAsync() => _connStr = await fixture.CreateDatabaseAsync("invalidator");

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private MasterDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_connStr, ServerVersion.AutoDetect(_connStr))
            .Options;
        return new MasterDbContext(options);
    }

    [Fact]
    public async Task InvalidateRoleAsync_RemovesCacheForAllUsersWithThatRole()
    {
        // master DB seed: role + 2 user + user-role atamasi
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();

            var role = new Role { Name = "editor", Description = "test", CreatedAtUtc = DateTime.UtcNow };
            setup.Roles.Add(role);
            var u1 = new User { Email = "u1@x.com", DisplayName = "u1", PasswordHash = "h", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            var u2 = new User { Email = "u2@x.com", DisplayName = "u2", PasswordHash = "h", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            setup.Users.AddRange(u1, u2);
            await setup.SaveChangesAsync();

            setup.UserRoles.AddRange(
                new UserRole { UserId = u1.Id, RoleId = role.Id, TenantId = null, AssignedAtUtc = DateTime.UtcNow },
                new UserRole { UserId = u2.Id, RoleId = role.Id, TenantId = null, AssignedAtUtc = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        int u1Id, u2Id, roleId;
        await using (var probe = CreateContext())
        {
            u1Id = await probe.Users.Where(u => u.Email == "u1@x.com").Select(u => u.Id).FirstAsync();
            u2Id = await probe.Users.Where(u => u.Email == "u2@x.com").Select(u => u.Id).FirstAsync();
            roleId = await probe.Roles.Where(r => r.Name == "editor").Select(r => r.Id).FirstAsync();
        }

        // Scope factory: master DB resolve eden minimum DI
        var sp = new ServiceCollection()
            .AddMemoryCache()
            .AddDbContext<MasterDbContext>(o => o.UseMySql(_connStr, ServerVersion.AutoDetect(_connStr)))
            .BuildServiceProvider();

        var cache = sp.GetRequiredService<IMemoryCache>();
        var invalidator = new MemoryPermissionCacheInvalidator(
            cache, sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<MemoryPermissionCacheInvalidator>.Instance);

        // Iki user icin shadow-index entry'leri olustur
        var u1Key = $"cms:perm:{u1Id}:global";
        var u2Key = $"cms:perm:{u2Id}:global";
        cache.Set(u1Key, new HashSet<string>(["a"]));
        cache.Set(u2Key, new HashSet<string>(["b"]));
        invalidator.RegisteredKeysSnapshot().Should().BeEmpty(); // henuz Register cagrilmadi
        invalidator.Register(u1Key);
        invalidator.Register(u2Key);

        await invalidator.InvalidateRoleAsync(roleId);

        cache.TryGetValue(u1Key, out _).Should().BeFalse();
        cache.TryGetValue(u2Key, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Register_ThenCacheRemove_TriggersUnregister_ViaPostEvictionCallback()
    {
        var sp = new ServiceCollection().AddMemoryCache().BuildServiceProvider();
        var cache = sp.GetRequiredService<IMemoryCache>();
        var invalidator = new MemoryPermissionCacheInvalidator(
            cache, new NoopScopeFactory(), NullLogger<MemoryPermissionCacheInvalidator>.Instance);

        var key = "cms:perm:42:global";

        // CachedPermissionService'in yaptigi gibi: post-eviction callback ile shadow index temizleme
        var opts = new MemoryCacheEntryOptions();
        opts.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string s)
            {
                invalidator.Unregister(s);
            }
        });
        cache.Set(key, "set", opts);
        invalidator.Register(key);

        invalidator.RegisteredKeysSnapshot().Should().Contain(key);

        cache.Remove(key);

        // Post-eviction callback'leri MemoryCache Task.Factory.StartNew ile async firlatir;
        // shadow index temizlenmesi icin kisaca bekle.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && invalidator.RegisteredKeysSnapshot().Contains(key))
        {
            await Task.Delay(20);
        }

        invalidator.RegisteredKeysSnapshot().Should().NotContain(key);
    }

    private sealed class NoopScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }
}
