namespace Cms.Core.Modules;

using Cms.Abstractions.Modules;

public sealed class ModuleDependencyResolver
{
    /// <summary>
    /// Topological sort + dependency validation. Donus: yukleme sirasinda gerekli modul listesi.
    /// </summary>
    /// <exception cref="InvalidOperationException">Cycle veya cozumlenemeyen dependency var ise.</exception>
    public static IReadOnlyList<ModuleDescriptor> Resolve(IReadOnlyList<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var byId = modules.ToDictionary(m => m.Manifest.Id);
        var sorted = new List<ModuleDescriptor>();
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        var orderedSeed = modules
            .OrderByDescending(m => m.Manifest.IsCorePlugin)
            .ThenBy(m => m.Manifest.Id, StringComparer.Ordinal);

        foreach (var module in orderedSeed)
        {
            Visit(module, byId, visiting, visited, sorted);
        }

        return sorted;
    }

    private static void Visit(
        ModuleDescriptor module,
        IReadOnlyDictionary<string, ModuleDescriptor> byId,
        HashSet<string> visiting,
        HashSet<string> visited,
        List<ModuleDescriptor> sorted)
    {
        var id = module.Manifest.Id;
        if (visited.Contains(id))
        {
            return;
        }
        if (!visiting.Add(id))
        {
            throw new InvalidOperationException(
                $"Modul bagimlilik dongusu (cycle) tespit edildi: {id}");
        }

        foreach (var dep in module.Manifest.Dependencies)
        {
            if (!byId.TryGetValue(dep.ModuleId, out var target))
            {
                if (dep.IsOptional)
                {
                    continue;
                }
                throw new InvalidOperationException(
                    $"Modul '{id}' bagimliligi '{dep.ModuleId}' bulunamadi.");
            }

            if (!dep.VersionRange.Satisfies(target.Manifest.Version))
            {
                throw new InvalidOperationException(
                    $"Modul '{id}' bagimliligi '{dep.ModuleId}' {dep.VersionRange} ister, " +
                    $"sistemde {target.Manifest.Version} var.");
            }

            Visit(target, byId, visiting, visited, sorted);
        }

        visiting.Remove(id);
        visited.Add(id);
        sorted.Add(module);
    }
}
