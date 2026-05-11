namespace Cms.Core.Modules;

using System.Collections.Concurrent;

public sealed class ModuleDescriptorRegistry
{
    private IReadOnlyList<ModuleDescriptor> _modules = [];
    private readonly ConcurrentDictionary<Type, byte> _softDeletableTypes = new();

    public IReadOnlyList<ModuleDescriptor> Modules => _modules;

    /// <summary>
    /// ISoftDeletable implement eden, modul-kayit-zamani sirasinda <see cref="Cms.Core.Data.TenantDbContext"/>
    /// tarafindan toplanan entity tipleri. SoftDeleteController bu listeyi entity dropdown'i icin kullanir.
    /// Concurrent: birden cok TenantDbContext OnModelCreating'i paralel cagirabilir.
    /// </summary>
    public IReadOnlyCollection<Type> GetSoftDeletableEntityTypes() => [.. _softDeletableTypes.Keys];

    internal void Set(IReadOnlyList<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
    }

    internal void RegisterSoftDeletableTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        foreach (var t in types)
        {
            _softDeletableTypes.TryAdd(t, 0);
        }
    }
}
