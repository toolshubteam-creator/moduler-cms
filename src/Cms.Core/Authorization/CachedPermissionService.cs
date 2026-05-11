namespace Cms.Core.Authorization;

using System.Collections.Frozen;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// IPermissionService decorator. Underlying PermissionService cevabini per-(userId, tenantId)
/// FrozenSet olarak IMemoryCache'te 5dk sliding TTL ile tutar. Cache miss path'inde
/// invalidator shadow index'ine kayit eder; post-eviction callback ile cikartir.
///
/// SuperAdmin (IsSystem=true rol) bypass davranisi underlying'de korunur: o kullanici icin
/// dondurulen set tum permission'lari icerir, Contains lookup yine dogru calisir.
///
/// Thread-safety: FrozenSet okuma immutable + thread-safe. Cache populate sirasinda paralel
/// iki miss ikinci kez SQL atar ve set'i overwrite eder — kabul edilebilir race.
/// </summary>
public sealed class CachedPermissionService(
    IPermissionService underlying,
    IMemoryCache cache,
    MemoryPermissionCacheInvalidator invalidator,
    ILogger<CachedPermissionService> logger) : IPermissionService
{
    private static readonly TimeSpan _slidingTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> HasPermissionAsync(
        int userId,
        Guid? tenantId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        var set = await GetUserPermissionsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        return set.Contains(permissionKey.Trim().ToLowerInvariant());
    }

    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        int userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId, tenantId);

        if (cache.TryGetValue<FrozenSet<string>>(key, out var cached) && cached is not null)
        {
            logger.LogDebug("Permission cache HIT {Key}", key);
            return cached;
        }

        logger.LogDebug("Permission cache MISS {Key}", key);
        var fresh = await underlying.GetUserPermissionsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var frozen = fresh.ToFrozenSet(StringComparer.Ordinal);

        var entryOptions = new MemoryCacheEntryOptions { SlidingExpiration = _slidingTtl };
        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string s)
            {
                invalidator.Unregister(s);
            }
        });
        cache.Set(key, frozen, entryOptions);
        invalidator.Register(key);

        return frozen;
    }

    internal static string BuildKey(int userId, Guid? tenantId) =>
        $"{MemoryPermissionCacheInvalidator.KeyPrefix}{userId}:{tenantId?.ToString() ?? "global"}";
}
