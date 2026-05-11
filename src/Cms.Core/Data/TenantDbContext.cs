namespace Cms.Core.Data;

using Cms.Abstractions.Modules;
using Cms.Core.Domain.Auditing;
using Cms.Core.Extensions;
using Cms.Core.Modules;
using Microsoft.EntityFrameworkCore;

public class TenantDbContext : DbContext
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules;
    private readonly ModuleDescriptorRegistry? _registry;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, IReadOnlyList<ModuleDescriptor> modules)
        : this(options, modules, registry: null)
    {
    }

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        IReadOnlyList<ModuleDescriptor> modules,
        ModuleDescriptorRegistry? registry)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
        _registry = registry;
        ModuleSetCacheKey = modules.Count == 0
            ? string.Empty
            : string.Join("|", modules.Select(m => m.Manifest.Id).OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>
    /// Model cache anahtarinda modul setinin imzasi. <see cref="TenantDbContextModelCacheKeyFactory"/>
    /// farkli modul kombinasyonlarinin ayri cache slot'larina dusmesini saglar.
    /// </summary>
    internal string ModuleSetCacheKey { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("Audit_Entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            b.Property(x => x.Action).HasConversion<int>().IsRequired();
            b.Property(x => x.UserId);
            b.Property(x => x.Timestamp).IsRequired();
            b.Property(x => x.Changes).HasColumnType("json");
            b.HasIndex(x => new { x.EntityName, x.EntityId });
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.Timestamp).IsDescending();
        });

        foreach (var module in _modules)
        {
            if (module.Instance is IHasEntities hasEntities)
            {
                hasEntities.RegisterEntities(modelBuilder);
            }
        }

        modelBuilder.ApplySoftDeleteFilters();

        base.OnModelCreating(modelBuilder);

        // NOT: ISoftDeletable tip toplama OnModelCreating'de YAPILMAZ — EF Core
        // model cache'i nedeniyle yalniz ilk cache miss'inde calisirdi (cache key
        // ayni modul set'i icin sabit, registry instance test bazli degiserdi).
        // TenantDbContextFactory.Create her cagrida ctx.Model uzerinden registry'ye
        // populate ediyor; bu konum daha guvenilir.
    }
}
