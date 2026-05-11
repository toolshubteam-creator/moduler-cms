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
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// IAuditable entity'lerin her Create/Update/Delete/Restore operasyonu icin Audit_Entries
/// tablosuna bir kayit uretir. SoftDeleteInterceptor'dan SONRA cagrildigi icin
/// soft-deleted entity'lerin State'i Modified, IsDeleted false->true gecisi ile yakalanip
/// AuditAction.Delete olarak siniflandirilir.
///
/// Snapshot 2-fazli yazilir: SavingChangesAsync pending listeyi (per-DbContext, ConditionalWeakTable)
/// olusturur, SavedChangesAsync auto-increment PK'lar populate olduktan sonra AuditEntry'leri
/// olusturur ve ayrica bir SaveChangesAsync ile persist eder.
///
/// D-017: Main entity save + audit row insert atomicligi icin interceptor SavingChanges'te
/// outer transaction yoksa OWNED transaction acar; SavedChanges'te commit, SaveChangesFailedAsync'te
/// rollback. Caller (orn. TenantProvisioningService) zaten transaction acmissa interceptor
/// no-op (CurrentTransaction != null) — caller'in sorumluluguna birakilir.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IServiceProvider rootProvider) : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> _ignoredCache = new();
    private static readonly ConditionalWeakTable<DbContext, AuditContextState> _state = new();

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
        var state = CollectPending(eventData.Context);
        BeginOwnedTransactionIfNeeded(eventData.Context, state);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var state = CollectPending(eventData.Context);
        await BeginOwnedTransactionIfNeededAsync(eventData.Context, state, cancellationToken).ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
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

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        AbortOwnedTransaction(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await AbortOwnedTransactionAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        await base.SaveChangesFailedAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    private AuditContextState? CollectPending(DbContext? ctx)
    {
        if (ctx is null)
        {
            return null;
        }

        var state = GetOrCreateState(ctx);

        // Nested call: bir onceki SaveChanges butun snapshot listesini hala tasiyabilir
        // (SavedChanges icindeki ikinci SaveChangesAsync recursive cagri yapar).
        // Nested cagrida pending zaten flush edildiyse veya bos baslangic — guvenli.
        var userId = ResolveUserId();
        var now = DateTime.UtcNow;

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
                state.PendingAudits.Add(new PendingAudit(entry.Metadata.ClrType.Name, action, entry, null, userId, now, changes));
            }
            else
            {
                var pk = ReadPrimaryKey(entry, useOriginal: entry.State == EntityState.Deleted);
                state.PendingAudits.Add(new PendingAudit(entry.Metadata.ClrType.Name, action, null, pk, userId, now, changes));
            }
        }

        return state;
    }

    private static AuditContextState GetOrCreateState(DbContext ctx)
    {
        if (_state.TryGetValue(ctx, out var existing))
        {
            return existing;
        }

        var fresh = new AuditContextState();
        _state.Add(ctx, fresh);
        return fresh;
    }

    private static void BeginOwnedTransactionIfNeeded(DbContext? ctx, AuditContextState? state)
    {
        if (ctx is null || state is null || state.PendingAudits.Count == 0)
        {
            return;
        }
        if (ctx.Database.CurrentTransaction is not null)
        {
            return;
        }
        if (state.OwnedTransaction is not null)
        {
            return;
        }
        state.OwnedTransaction = ctx.Database.BeginTransaction();
    }

    private static async ValueTask BeginOwnedTransactionIfNeededAsync(
        DbContext? ctx, AuditContextState? state, CancellationToken cancellationToken)
    {
        if (ctx is null || state is null || state.PendingAudits.Count == 0)
        {
            return;
        }
        if (ctx.Database.CurrentTransaction is not null)
        {
            return;
        }
        if (state.OwnedTransaction is not null)
        {
            return;
        }
        state.OwnedTransaction = await ctx.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void FlushPending(DbContext? ctx)
    {
        if (ctx is null || !_state.TryGetValue(ctx, out var state) || state.PendingAudits.Count == 0)
        {
            return;
        }

        var pending = state.PendingAudits.ToList();
        state.PendingAudits.Clear();

        try
        {
            AddAuditEntries(ctx, pending);
            ctx.SaveChanges();
            CommitOwnedTransaction(state);
        }
        catch
        {
            AbortOwnedTransactionInternal(state);
            throw;
        }
    }

    private static async Task FlushPendingAsync(DbContext? ctx, CancellationToken cancellationToken)
    {
        if (ctx is null || !_state.TryGetValue(ctx, out var state) || state.PendingAudits.Count == 0)
        {
            return;
        }

        var pending = state.PendingAudits.ToList();
        state.PendingAudits.Clear();

        try
        {
            AddAuditEntries(ctx, pending);
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await CommitOwnedTransactionAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await AbortOwnedTransactionInternalAsync(state, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void CommitOwnedTransaction(AuditContextState state)
    {
        if (state.OwnedTransaction is null)
        {
            return;
        }
        try
        {
            state.OwnedTransaction.Commit();
        }
        finally
        {
            state.OwnedTransaction.Dispose();
            state.OwnedTransaction = null;
        }
    }

    private static async ValueTask CommitOwnedTransactionAsync(AuditContextState state, CancellationToken cancellationToken)
    {
        if (state.OwnedTransaction is null)
        {
            return;
        }
        try
        {
            await state.OwnedTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await state.OwnedTransaction.DisposeAsync().ConfigureAwait(false);
            state.OwnedTransaction = null;
        }
    }

    private static void AbortOwnedTransaction(DbContext? ctx)
    {
        if (ctx is null || !_state.TryGetValue(ctx, out var state))
        {
            return;
        }

        state.PendingAudits.Clear();
        AbortOwnedTransactionInternal(state);
    }

    private static async Task AbortOwnedTransactionAsync(DbContext? ctx, CancellationToken cancellationToken)
    {
        if (ctx is null || !_state.TryGetValue(ctx, out var state))
        {
            return;
        }

        state.PendingAudits.Clear();
        await AbortOwnedTransactionInternalAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private static void AbortOwnedTransactionInternal(AuditContextState state)
    {
        if (state.OwnedTransaction is null)
        {
            return;
        }
        try
        {
            state.OwnedTransaction.Rollback();
        }
        catch
        {
            // Rollback patlarsa swallow — exception zaten propagation halinde.
        }
        finally
        {
            state.OwnedTransaction.Dispose();
            state.OwnedTransaction = null;
        }
    }

    private static async Task AbortOwnedTransactionInternalAsync(AuditContextState state, CancellationToken cancellationToken)
    {
        if (state.OwnedTransaction is null)
        {
            return;
        }
        try
        {
            await state.OwnedTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Rollback patlarsa swallow — exception zaten propagation halinde.
        }
        finally
        {
            await state.OwnedTransaction.DisposeAsync().ConfigureAwait(false);
            state.OwnedTransaction = null;
        }
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

    private sealed class AuditContextState
    {
        public List<PendingAudit> PendingAudits { get; } = [];

        public IDbContextTransaction? OwnedTransaction { get; set; }
    }

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
