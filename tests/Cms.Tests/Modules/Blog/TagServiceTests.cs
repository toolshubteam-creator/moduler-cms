namespace Cms.Tests.Modules.Blog;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Modules.Blog;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Cms.Modules.Blog.Services;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class TagServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 7 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("blogtag");

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var asm = typeof(BlogModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new() { Instance = new BlogModule(), Assembly = asm, DllPath = asm.Location },
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

    private static TagService Svc(TenantDbContext ctx) => new(ctx);

    [Fact]
    public async Task CreateAsync_AutoGeneratesSlugFromName()
    {
        TagDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await Svc(ctx).CreateAsync(new CreateTagRequest(".NET 10", null));
        }
        dto.Slug.Should().Be("net-10");
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_AppendsSuffix()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await Svc(ctx).CreateAsync(new CreateTagRequest("Dotnet", null));
        }
        TagDto second;
        await using (var ctx = _factory.Create(_connStr))
        {
            second = await Svc(ctx).CreateAsync(new CreateTagRequest("Dotnet", null));
        }
        second.Slug.Should().Be("dotnet-2");
    }

    [Fact]
    public async Task DeleteAsync_HardDeletes()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            id = (await Svc(ctx).CreateAsync(new CreateTagRequest("Tmp", null))).Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await Svc(ctx).DeleteAsync(id);
        }

        await using var verify = _factory.Create(_connStr);
        var raw = await verify.Set<BlogTag>().FirstOrDefaultAsync(t => t.Id == id);
        raw.Should().BeNull("hard delete sonrasi kayit yok olmali");
    }

    [Fact]
    public async Task DeleteAsync_WithJunctionLink_CascadesRemoval()
    {
        // Tag → post junction yarat → tag delete → junction satiri da silinmeli (FK CASCADE).
        int tagId;
        int postId;
        await using (var ctx = _factory.Create(_connStr))
        {
            tagId = (await Svc(ctx).CreateAsync(new CreateTagRequest("temp", null))).Id;
            var post = new BlogPost { Title = "T", Slug = "t", Content = "c", AuthorUserId = 1 };
            ctx.Set<BlogPost>().Add(post);
            await ctx.SaveChangesAsync();
            postId = post.Id;
            ctx.Set<BlogPostTag>().Add(new BlogPostTag { PostId = postId, TagId = tagId });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            await Svc(ctx).DeleteAsync(tagId);
        }

        await using var verify = _factory.Create(_connStr);
        var junctionCount = await verify.Set<BlogPostTag>().CountAsync(pt => pt.PostId == postId);
        junctionCount.Should().Be(0, "Tag hard-delete sonrasi junction FK CASCADE ile silindi");
    }

    [Fact]
    public async Task GetOrCreateManyAsync_NewNames_AreCreated()
    {
        IReadOnlyList<TagDto> tags;
        await using (var ctx = _factory.Create(_connStr))
        {
            tags = await Svc(ctx).GetOrCreateManyAsync(new[] { "alpha", "beta" });
        }
        tags.Should().HaveCount(2);

        await using var verify = _factory.Create(_connStr);
        var dbCount = await verify.Set<BlogTag>().CountAsync();
        dbCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreateManyAsync_ExistingNames_AreReused()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await Svc(ctx).CreateAsync(new CreateTagRequest("dotnet", null));
        }

        IReadOnlyList<TagDto> tags;
        await using (var ctx = _factory.Create(_connStr))
        {
            tags = await Svc(ctx).GetOrCreateManyAsync(new[] { "dotnet", "csharp" });
        }
        tags.Should().HaveCount(2);

        await using var verify = _factory.Create(_connStr);
        var dbCount = await verify.Set<BlogTag>().CountAsync();
        dbCount.Should().Be(2, "dotnet zaten vardi");
    }

    [Fact]
    public async Task GetOrCreateManyAsync_DuplicateInputNames_AreNormalized()
    {
        // Case + whitespace varyasyonlari ayni slug'a duser → tek satir.
        IReadOnlyList<TagDto> tags;
        await using (var ctx = _factory.Create(_connStr))
        {
            tags = await Svc(ctx).GetOrCreateManyAsync(new[] { "csharp", "CSHARP", "  csharp  " });
        }
        tags.Should().HaveCount(1, "ayni slug'a duşen girdiler tekillestirilmeli");

        await using var verify = _factory.Create(_connStr);
        var dbCount = await verify.Set<BlogTag>().CountAsync();
        dbCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateManyAsync_EmptyAndWhitespace_AreSkipped()
    {
        IReadOnlyList<TagDto> tags;
        await using (var ctx = _factory.Create(_connStr))
        {
            tags = await Svc(ctx).GetOrCreateManyAsync(new[] { "", "  ", "!!!", "dotnet" });
        }
        tags.Should().HaveCount(1);
        tags[0].Slug.Should().Be("dotnet");
    }
}
