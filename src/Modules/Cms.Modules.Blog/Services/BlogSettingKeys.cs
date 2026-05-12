namespace Cms.Modules.Blog.Services;

public static class BlogSettingKeys
{
    public const string UrlPattern = "blog.url_pattern";
    public const string PostsPerPage = "blog.posts_per_page";
    public const string DefaultMetaTitle = "blog.default_meta_title";
    public const string DefaultMetaDescription = "blog.default_meta_description";
    public const string ShowExcerptInList = "blog.show_excerpt_in_list";
}

public static class BlogSettingDefaults
{
    public const string UrlPattern = "/blog/{slug}";
    public const int PostsPerPage = 10;
    public const int PostsPerPageMin = 1;
    public const int PostsPerPageMax = 100;
    public const bool ShowExcerptInList = true;
}
