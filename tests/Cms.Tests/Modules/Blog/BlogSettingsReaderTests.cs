namespace Cms.Tests.Modules.Blog;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Modules.Blog.Services;
using Cms.Modules.Settings;
using Cms.Modules.Settings.Services;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class BlogSettingsReaderTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 7 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("blogsettings");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var settingsAsm = typeof(SettingsModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new() { Instance = new SettingsModule(), Assembly = settingsAsm, DllPath = settingsAsm.Location },
        };
        var interceptors = new IInterceptor[]
        {
            new SoftDeleteInterceptor(),
            new AuditSaveChangesInterceptor(sp),
        };
        _factory = new TenantDbContextFactory(modules, interceptors);

        await using var ctx = _factory.Create(_connStr);
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private BlogSettingsReader CreateReader(TenantDbContext ctx) => new(new SettingsService(ctx));
    private static SettingsService SettingsServiceFor(TenantDbContext ctx) => new(ctx);

    [Fact]
    public async Task GetAsync_EmptyDatabase_ReturnsDefaults()
    {
        await using var ctx = _factory.Create(_connStr);
        var snap = await CreateReader(ctx).GetAsync();

        snap.UrlPattern.Should().Be(BlogSettingDefaults.UrlPattern);
        snap.PostsPerPage.Should().Be(BlogSettingDefaults.PostsPerPage);
        snap.DefaultMetaTitle.Should().BeNull();
        snap.DefaultMetaDescription.Should().BeNull();
        snap.ShowExcerptInList.Should().Be(BlogSettingDefaults.ShowExcerptInList);
    }

    [Fact]
    public async Task GetAsync_PostsPerPageInvalid_FallsBackToDefault()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await SettingsServiceFor(ctx).SetAsync(BlogSettingKeys.PostsPerPage, -1);
        }

        await using var read = _factory.Create(_connStr);
        var snap = await CreateReader(read).GetAsync();
        snap.PostsPerPage.Should().Be(BlogSettingDefaults.PostsPerPage);
    }

    [Fact]
    public async Task GetAsync_PostsPerPageTooLarge_FallsBackToDefault()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await SettingsServiceFor(ctx).SetAsync(BlogSettingKeys.PostsPerPage, 5000);
        }

        await using var read = _factory.Create(_connStr);
        var snap = await CreateReader(read).GetAsync();
        snap.PostsPerPage.Should().Be(BlogSettingDefaults.PostsPerPage);
    }

    [Fact]
    public async Task GetAsync_AllValuesSet_ReturnsConfiguredValues()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            var sut = SettingsServiceFor(ctx);
            await sut.SetAsync(BlogSettingKeys.UrlPattern, "/articles/{slug}");
            await sut.SetAsync(BlogSettingKeys.PostsPerPage, 25);
            await sut.SetAsync(BlogSettingKeys.DefaultMetaTitle, "Acme Blog");
            await sut.SetAsync(BlogSettingKeys.DefaultMetaDescription, "Acme corp updates");
            await sut.SetAsync(BlogSettingKeys.ShowExcerptInList, false);
        }

        await using var read = _factory.Create(_connStr);
        var snap = await CreateReader(read).GetAsync();

        snap.UrlPattern.Should().Be("/articles/{slug}");
        snap.PostsPerPage.Should().Be(25);
        snap.DefaultMetaTitle.Should().Be("Acme Blog");
        snap.DefaultMetaDescription.Should().Be("Acme corp updates");
        snap.ShowExcerptInList.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_ShowExcerptFalse_ReturnsFalse()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await SettingsServiceFor(ctx).SetAsync(BlogSettingKeys.ShowExcerptInList, false);
        }

        await using var read = _factory.Create(_connStr);
        var snap = await CreateReader(read).GetAsync();
        snap.ShowExcerptInList.Should().BeFalse();
    }
}
