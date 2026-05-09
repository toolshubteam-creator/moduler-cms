namespace Cms.Abstractions.Modules.Permissions;

public sealed record PermissionDescriptor
{
    /// <summary>Modul-prefix'li izin kimligi. Ornek: "blog.posts.create".</summary>
    public required string Key { get; init; }

    /// <summary>Insan-okur baslik.</summary>
    public required string DisplayName { get; init; }

    public string? Description { get; init; }
}
