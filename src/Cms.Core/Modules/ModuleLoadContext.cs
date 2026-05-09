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

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var hostAssembly = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (hostAssembly is not null)
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
