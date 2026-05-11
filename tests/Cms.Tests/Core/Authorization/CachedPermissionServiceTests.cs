namespace Cms.Tests.Core.Authorization;

using Cms.Core.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class CachedPermissionServiceTests
{
    private static (CachedPermissionService Sut, IMemoryCache Cache, MemoryPermissionCacheInvalidator Invalidator, CountingPermissionService Underlying)
        BuildSut(IReadOnlySet<string>? returnSet = null)
    {
        var sp = new ServiceCollection().AddMemoryCache().BuildServiceProvider();
        var cache = sp.GetRequiredService<IMemoryCache>();
        var underlying = new CountingPermissionService(returnSet ?? new HashSet<string>(["blog.posts.create"]));
        var invalidator = new MemoryPermissionCacheInvalidator(
            cache,
            new NullScopeFactory(),
            NullLogger<MemoryPermissionCacheInvalidator>.Instance);
        var sut = new CachedPermissionService(underlying, cache, invalidator, NullLogger<CachedPermissionService>.Instance);
        return (sut, cache, invalidator, underlying);
    }

    [Fact]
    public async Task HasPermissionAsync_FirstCall_QueriesUnderlying()
    {
        var (sut, _, _, underlying) = BuildSut();

        var ok = await sut.HasPermissionAsync(1, null, "blog.posts.create");

        ok.Should().BeTrue();
        underlying.GetCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HasPermissionAsync_SecondCall_UsesCachedSet()
    {
        var (sut, _, _, underlying) = BuildSut();

        await sut.HasPermissionAsync(1, null, "blog.posts.create");
        await sut.HasPermissionAsync(1, null, "blog.posts.delete");
        var ok = await sut.HasPermissionAsync(1, null, "blog.posts.create");

        ok.Should().BeTrue();
        underlying.GetCallCount.Should().Be(1, "ikinci ve sonraki cagrilar cache'ten dondu");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Returns_CachedSetOnRepeatCalls()
    {
        var (sut, _, _, underlying) = BuildSut();

        var first = await sut.GetUserPermissionsAsync(7, null);
        var second = await sut.GetUserPermissionsAsync(7, null);

        first.Should().BeEquivalentTo(second);
        underlying.GetCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateUser_RemovesAllUserCacheEntries()
    {
        var (sut, _, invalidator, underlying) = BuildSut();
        var tenantA = Guid.NewGuid();

        await sut.GetUserPermissionsAsync(1, tenantA);
        await sut.GetUserPermissionsAsync(1, null);
        await sut.GetUserPermissionsAsync(2, null);
        underlying.GetCallCount.Should().Be(3);

        invalidator.InvalidateUser(1);

        // user 1 cache'ten silindi -> underlying tekrar cagirilir
        await sut.GetUserPermissionsAsync(1, tenantA);
        await sut.GetUserPermissionsAsync(1, null);
        // user 2 cache'te kalmali
        await sut.GetUserPermissionsAsync(2, null);

        underlying.GetCallCount.Should().Be(5, "user 1 icin 2 cagri eklenir, user 2 cache'te");
    }

    [Fact]
    public async Task InvalidateAll_ClearsEverything()
    {
        var (sut, _, invalidator, underlying) = BuildSut();

        await sut.GetUserPermissionsAsync(1, null);
        await sut.GetUserPermissionsAsync(2, null);
        underlying.GetCallCount.Should().Be(2);

        invalidator.InvalidateAll();

        await sut.GetUserPermissionsAsync(1, null);
        await sut.GetUserPermissionsAsync(2, null);

        underlying.GetCallCount.Should().Be(4);
    }

    private sealed class CountingPermissionService(IReadOnlySet<string> set) : IPermissionService
    {
        public int GetCallCount { get; private set; }

        public Task<bool> HasPermissionAsync(int userId, Guid? tenantId, string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(set.Contains(permissionKey));

        public Task<IReadOnlySet<string>> GetUserPermissionsAsync(int userId, Guid? tenantId, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(set);
        }
    }

    private sealed class NullScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException("InvalidateRoleAsync bu testte kullanilmiyor.");
    }
}
