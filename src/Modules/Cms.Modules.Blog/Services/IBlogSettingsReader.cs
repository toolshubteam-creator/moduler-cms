namespace Cms.Modules.Blog.Services;

public interface IBlogSettingsReader
{
    Task<BlogSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
