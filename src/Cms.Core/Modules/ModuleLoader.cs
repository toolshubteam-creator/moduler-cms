namespace Cms.Core.Modules;

using System.Reflection;
using Cms.Abstractions.Modules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ModuleLoader : IModuleLoader
{
    private readonly ModuleLoaderOptions _options;
    private readonly ILogger<ModuleLoader> _logger;

    public ModuleLoader(
        IOptions<ModuleLoaderOptions> options,
        ILogger<ModuleLoader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<ModuleDescriptor> LoadAll()
    {
        if (!Directory.Exists(_options.Path))
        {
            _logger.LogWarning("Modul dizini bulunamadi: {Path}", _options.Path);
            return [];
        }

        var dllPaths = Directory.GetFiles(_options.Path, _options.SearchPattern, SearchOption.AllDirectories);
        var descriptors = new List<ModuleDescriptor>();

        foreach (var dllPath in dllPaths)
        {
            var descriptor = TryLoadModule(dllPath);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return ModuleDependencyResolver.Resolve(descriptors);
    }

    private ModuleDescriptor? TryLoadModule(string dllPath)
    {
        try
        {
            var context = new ModuleLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);

            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface && typeof(IModule).IsAssignableFrom(t));

            if (moduleType is null)
            {
                _logger.LogDebug("DLL '{Dll}' icinde IModule implementasyonu yok, atlaniyor.", dllPath);
                return null;
            }

            var instance = (IModule)Activator.CreateInstance(moduleType)!;

            return new ModuleDescriptor
            {
                Instance = instance,
                Assembly = assembly,
                DllPath = dllPath
            };
        }
        catch (Exception ex) when (ex is BadImageFormatException or ReflectionTypeLoadException or FileLoadException)
        {
            _logger.LogWarning(ex, "DLL '{Dll}' yuklenemedi, atlaniyor.", dllPath);
            return null;
        }
    }
}
