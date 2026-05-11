namespace Cms.Core.Authorization;

using System.Collections.Concurrent;
using Cms.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// IMemoryCache wildcard remove desteklemediginden permission cache key'leri ayrica bir
/// shadow index'te (ConcurrentDictionary) tutulur. CachedPermissionService cache koyarken
/// Register, post-eviction callback'inde Unregister cagirir. InvalidateUser/InvalidateAll
/// shadow index uzerinden ilgili cache key'lerini hesaplar ve IMemoryCache.Remove eder.
/// </summary>
public sealed class MemoryPermissionCacheInvalidator(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    ILogger<MemoryPermissionCacheInvalidator> logger) : IPermissionCacheInvalidator
{
    public const string KeyPrefix = "cms:perm:";

    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    internal void Register(string key) => _keys.TryAdd(key, 0);

    internal void Unregister(string key) => _keys.TryRemove(key, out _);

    internal IReadOnlyCollection<string> RegisteredKeysSnapshot() => [.. _keys.Keys];

    public void InvalidateUser(int userId)
    {
        var userPrefix = $"{KeyPrefix}{userId}:";
        var matched = _keys.Keys
            .Where(k => k.StartsWith(userPrefix, StringComparison.Ordinal))
            .ToList();
        foreach (var key in matched)
        {
            cache.Remove(key);
        }
        logger.LogDebug("Invalidated {Count} cache entries for user {UserId}", matched.Count, userId);
    }

    public async Task InvalidateRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var master = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var userIds = await master.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var uid in userIds)
        {
            InvalidateUser(uid);
        }
        logger.LogDebug("Invalidated cache for {UserCount} users of role {RoleId}", userIds.Count, roleId);
    }

    public void InvalidateAll()
    {
        var snapshot = _keys.Keys.ToList();
        foreach (var key in snapshot)
        {
            cache.Remove(key);
        }
        logger.LogDebug("Invalidated all permission cache entries ({Count})", snapshot.Count);
    }
}
