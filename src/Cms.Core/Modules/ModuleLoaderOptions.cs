namespace Cms.Core.Modules;

public sealed class ModuleLoaderOptions
{
    public const string SectionName = "Modules";

    /// <summary>Modul DLL'lerinin arandigi dizin. Default: AppContext.BaseDirectory + "Modules".</summary>
    public string Path { get; set; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Modules");

    /// <summary>DLL search pattern. Default: "*.dll".</summary>
    public string SearchPattern { get; set; } = "*.dll";
}
