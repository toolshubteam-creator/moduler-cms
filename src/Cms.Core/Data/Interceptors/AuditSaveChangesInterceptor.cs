namespace Cms.Core.Data.Interceptors;

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cms.Core.Domain.Auditing;
using Cms.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// IAuditable entity'lerin her Create/Update/Delete/Restore operasyonu icin Audit_Entries
/// tablosuna bir kayit uretir. SoftDeleteInterceptor'dan SONRA cagrildigi icin
/// soft-deleted entity'lerin State'i Modified, IsDeleted false->true gecisi ile yakalanip
/// AuditAction.Delete olarak siniflandirilir.
///
/// Snapshot 2-fazli yazilir: SavingChangesAsync pending listeyi (per-DbContext, ConditionalWeakTable)
/// olusturur, SavedChangesAsync auto-increment PK'lar populate olduktan sonra AuditEntry'leri
/// olusturur ve ayrica bir SaveChangesAsync ile persist eder. Audit row main entity ile ayni
/// transaction'da degil, sonrasinda kayit edilir; partial-failure durumu Faz-3+ konusu.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IServiceProvider rootProvider) : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> _ignoredCache = new();
    private static readonly ConditionalWeakTable<DbContext, List<PendingAudit>> _pending = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IServiceProvider _rootProvider = rootProvider;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CollectPending(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectPending(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushPending(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushPendingAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private void CollectPending(DbContext? ctx)
    {
        if (ctx is null)
        {
            return;
        }

        var userId = ResolveUserId();
        var now = DateTime.UtcNow;
        var pending = new List<PendingAudit>();

        // AuditEntry kendisi IAuditable degil, bu yuzden Entries<IAuditable>() asla onu dondurmez —
        // self-reference koruma static tip kontrolu ile garantilidir.
        var entries = ctx.ChangeTracker.Entries<IAuditable>().ToList();

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var (action, changes) = Classify(entry);

            if (entry.State == EntityState.Added)
            {
                pending.Add(new PendingAudit(entry.Metadata.ClrType.Name, action, entry, null, userId, now, changes));
            }
            else
            {
                var pk = ReadPrimaryKey(entry, useOriginal: entry.State == EntityState.Deleted);
                pending.Add(new PendingAudit(entry.Metadata.ClrType.Name, action, null, pk, userId, now, changes));
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        _pending.Remove(ctx);
        _pending.Add(ctx, pending);
    }

    private static void FlushPending(DbContext? ctx)
    {
        if (ctx is null || !_pending.TryGetValue(ctx, out var pending) || pending.Count == 0)
        {
            return;
        }

        _pending.Remove(ctx);
        AddAuditEntries(ctx, pending);
        ctx.SaveChanges();
    }

    private static async Task FlushPendingAsync(DbContext? ctx, CancellationToken cancellationToken)
    {
        if (ctx is null || !_pending.TryGetValue(ctx, out var pending) || pending.Count == 0)
        {
            return;
        }

        _pending.Remove(ctx);
        AddAuditEntries(ctx, pending);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddAuditEntries(DbContext ctx, List<PendingAudit> pending)
    {
        foreach (var p in pending)
        {
            var entityId = p.AddedEntry is not null
                ? ReadPrimaryKey(p.AddedEntry, useOriginal: false)
                : p.EntityIdOverride ?? string.Empty;

            ctx.Set<AuditEntry>().Add(new AuditEntry
            {
                EntityName = p.EntityName,
                EntityId = entityId,
                Action = p.Action,
                UserId = p.UserId,
                Timestamp = p.Timestamp,
                Changes = p.Changes,
            });
        }
    }

    private int? ResolveUserId()
    {
        try
        {
            using var scope = _rootProvider.CreateScope();
            return scope.ServiceProvider.GetService<ICurrentUserService>()?.UserId;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static (AuditAction Action, string? Changes) Classify(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return (AuditAction.Create, null);

            case EntityState.Deleted:
                return (AuditAction.Delete, null);

            case EntityState.Modified:
                {
                    if (entry.Entity is ISoftDeletable)
                    {
                        var prop = entry.Property(nameof(ISoftDeletable.IsDeleted));
                        var wasDeleted = prop.OriginalValue is true;
                        var isDeleted = prop.CurrentValue is true;
                        if (!wasDeleted && isDeleted)
                        {
                            return (AuditAction.Delete, null);
                        }
                        if (wasDeleted && !isDeleted)
                        {
                            return (AuditAction.Restore, null);
                        }
                    }
                    return (AuditAction.Update, SerializeChanges(entry));
                }

            default:
                return (AuditAction.Update, null);
        }
    }

    private static string? SerializeChanges(EntityEntry entry)
    {
        var ignored = _ignoredCache.GetOrAdd(entry.Entity.GetType(), BuildIgnoredSet);
        var dict = new Dictionary<string, ChangePair>(StringComparer.Ordinal);

        foreach (var p in entry.Properties)
        {
            if (!p.IsModified)
            {
                continue;
            }
            var name = p.Metadata.Name;
            if (ignored.Contains(name))
            {
                continue;
            }
            dict[name] = new ChangePair(p.OriginalValue, p.CurrentValue);
        }

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, _jsonOptions);
    }

    private static IReadOnlySet<string> BuildIgnoredSet(Type type)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetCustomAttribute<AuditIgnoreAttribute>() is not null)
            {
                set.Add(p.Name);
            }
        }
        return set;
    }

    private static string ReadPrimaryKey(EntityEntry entry, bool useOriginal)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null || pk.Properties.Count == 0)
        {
            return string.Empty;
        }

        if (pk.Properties.Count == 1)
        {
            var pe = entry.Property(pk.Properties[0].Name);
            return Format(useOriginal ? pe.OriginalValue : pe.CurrentValue);
        }

        var parts = new List<string>(pk.Properties.Count);
        foreach (var p in pk.Properties)
        {
            var pe = entry.Property(p.Name);
            parts.Add(Format(useOriginal ? pe.OriginalValue : pe.CurrentValue));
        }
        return string.Join('|', parts);
    }

    private static string Format(object? value) =>
        value switch
        {
            null => string.Empty,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

    private sealed record PendingAudit(
        string EntityName,
        AuditAction Action,
        EntityEntry? AddedEntry,
        string? EntityIdOverride,
        int? UserId,
        DateTime Timestamp,
        string? Changes);

    private sealed record ChangePair(object? Old, object? New);
}
