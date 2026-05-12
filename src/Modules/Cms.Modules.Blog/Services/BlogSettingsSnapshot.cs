namespace Cms.Modules.Blog.Services;

public sealed record BlogSettingsSnapshot(
    string UrlPattern,
    int PostsPerPage,
    string? DefaultMetaTitle,
    string? DefaultMetaDescription,
    bool ShowExcerptInList);
