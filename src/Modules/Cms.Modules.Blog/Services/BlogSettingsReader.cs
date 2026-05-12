namespace Cms.Modules.Blog.Services;

using Cms.Modules.Settings.Contracts;

public sealed class BlogSettingsReader(ISettingsService settings) : IBlogSettingsReader
{
    public async Task<BlogSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var urlPattern = await settings.GetAsync<string>(BlogSettingKeys.UrlPattern, cancellationToken).ConfigureAwait(false);
        var postsPerPageRaw = await settings.GetAsync<int?>(BlogSettingKeys.PostsPerPage, cancellationToken).ConfigureAwait(false);
        var defaultMetaTitle = await settings.GetAsync<string>(BlogSettingKeys.DefaultMetaTitle, cancellationToken).ConfigureAwait(false);
        var defaultMetaDescription = await settings.GetAsync<string>(BlogSettingKeys.DefaultMetaDescription, cancellationToken).ConfigureAwait(false);
        var showExcerptRaw = await settings.GetAsync<bool?>(BlogSettingKeys.ShowExcerptInList, cancellationToken).ConfigureAwait(false);

        var postsPerPage = postsPerPageRaw ?? BlogSettingDefaults.PostsPerPage;
        if (postsPerPage < BlogSettingDefaults.PostsPerPageMin || postsPerPage > BlogSettingDefaults.PostsPerPageMax)
        {
            postsPerPage = BlogSettingDefaults.PostsPerPage;
        }

        return new BlogSettingsSnapshot(
            UrlPattern: string.IsNullOrWhiteSpace(urlPattern) ? BlogSettingDefaults.UrlPattern : urlPattern,
            PostsPerPage: postsPerPage,
            DefaultMetaTitle: string.IsNullOrWhiteSpace(defaultMetaTitle) ? null : defaultMetaTitle,
            DefaultMetaDescription: string.IsNullOrWhiteSpace(defaultMetaDescription) ? null : defaultMetaDescription,
            ShowExcerptInList: showExcerptRaw ?? BlogSettingDefaults.ShowExcerptInList);
    }
}
