namespace Cms.Core.Modules;

public interface IModuleLoader
{
    IReadOnlyList<ModuleDescriptor> LoadAll();
}
