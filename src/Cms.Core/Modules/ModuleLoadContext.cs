namespace Cms.Core.Modules;

using System.Reflection;
using System.Runtime.Loader;

internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public ModuleLoadContext(string moduleDllPath)
        : base(name: System.IO.Path.GetFileNameWithoutExtension(moduleDllPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(moduleDllPath);
    }

    /// <summary>
    /// Modul DLL'i ve modul-spesifik bagimliliklari (Cms.Modules.*.Contracts) bu ALC'ye yuklenir.
    /// Host'la paylasilan tum diger assembly'ler (Cms.Abstractions, Cms.Core, NuGet.Versioning,
    /// Microsoft.*, System.*) Default ALC'ye yonlendirilir — boylece tip identity tek olur ve
    /// modul kodu host'un ModuleManifest tipini iki farkli ALC'de gormez (MissingMethodException
    /// onlenir, Faz-4.1 calibration).
    ///
    /// Strateji: Default.LoadFromAssemblyName ile dene (host bin'inde varsa default'a yuklenir,
    /// null doneriz → modul ALC'de shared). Bulunamazsa local resolver (deps.json) ile modul ALC'ye yukle.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            _ = Default.LoadFromAssemblyName(assemblyName);
            return null;
        }
        catch (FileNotFoundException)
        {
        }
        catch (FileLoadException)
        {
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
