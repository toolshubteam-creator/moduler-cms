namespace Cms.Core.Modules;

public sealed class ModuleDescriptorRegistry
{
    private IReadOnlyList<ModuleDescriptor> _modules = [];

    public IReadOnlyList<ModuleDescriptor> Modules => _modules;

    internal void Set(IReadOnlyList<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
    }
}
