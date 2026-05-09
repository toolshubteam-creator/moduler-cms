namespace Cms.Core.Modules;

using System.Reflection;
using Cms.Abstractions.Modules;

public sealed record ModuleDescriptor
{
    public required IModule Instance { get; init; }
    public required Assembly Assembly { get; init; }
    public required string DllPath { get; init; }

    public ModuleManifest Manifest => Instance.Manifest;
}
