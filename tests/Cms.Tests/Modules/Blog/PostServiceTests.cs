namespace Cms.Tests.Modules.Blog;

using Cms.Core.Data;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Modules.Blog;
using Cms.Modules.Blog.Contracts;
using Cms.Modules.Blog.Domain;
using Cms.Modules.Blog.Services;
using Cms.Modules.Media.Contracts;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class PostServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 7 };
    private readonly StubMediaService _media = new();
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("blog");

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

    private PostService CreateService(TenantDbContext ctx) => new(ctx, _media, new TagService(ctx));

    private static CreatePostRequest NewRequest(
        string title = "Sample Post",
        string? slug = null,
        PostStatus status = PostStatus.Draft,
        int? featuredMediaId = null,
        int authorId = 7,
        string content = "Content body",
        string? excerpt = null,
        DateTime? publishAt = null,
        IReadOnlyList<int>? categoryIds = null,
        IReadOnlyList<string>? tagNames = null) =>
        new(title, slug, excerpt, content, status, publishAt, featuredMediaId, authorId,
            categoryIds ?? [], tagNames ?? []);

    [Fact]
    public async Task CreateAsync_AutoGeneratesSlugFromTitle_TurkishCharsNormalized()
    {
        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(title: "Üst Düzey Şuçukla İlgilenir"));
        }

        dto.Slug.Should().Be("ust-duzey-sucukla-ilgilenir");
    }

    [Fact]
    public async Task CreateAsync_ProvidedSlug_IsNormalizedAndUsed()
    {
        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(title: "Whatever", slug: "Özel  Slug!!"));
        }

        dto.Slug.Should().Be("ozel-slug");
    }

    [Fact]
    public async Task CreateAsync_DuplicateBaseSlug_AppendsSuffix2Then3()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).CreateAsync(NewRequest(title: "Hello World"));
        }
        PostDto second;
        await using (var ctx = _factory.Create(_connStr))
        {
            second = await CreateService(ctx).CreateAsync(NewRequest(title: "Hello World"));
        }
        PostDto third;
        await using (var ctx = _factory.Create(_connStr))
        {
            third = await CreateService(ctx).CreateAsync(NewRequest(title: "Hello World"));
        }

        second.Slug.Should().Be("hello-world-2");
        third.Slug.Should().Be("hello-world-3");
    }

    [Fact]
    public async Task CreateAsync_EmptyTitleAndNoSlug_Throws()
    {
        await using var ctx = _factory.Create(_connStr);
        var act = async () => await CreateService(ctx).CreateAsync(NewRequest(title: ""));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_TitleOnlySpecialChars_ThrowsSlugEmpty()
    {
        await using var ctx = _factory.Create(_connStr);
        var act = async () => await CreateService(ctx).CreateAsync(NewRequest(title: "!!!@@@$$$"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Slug*");
    }

    [Fact]
    public async Task CreateAsync_InvalidFeaturedMediaId_Throws()
    {
        await using var ctx = _factory.Create(_connStr);
        var act = async () => await CreateService(ctx).CreateAsync(NewRequest(featuredMediaId: 999));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*999*");
    }

    [Fact]
    public async Task CreateAsync_ValidFeaturedMediaId_StoresReference()
    {
        _media.Seed(new MediaFile(42, "x.jpg", "path", "image/jpeg", 100, "h", null, DateTime.UtcNow));

        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(featuredMediaId: 42));
        }

        dto.FeaturedMediaId.Should().Be(42);
    }

    [Fact]
    public async Task CreateAsync_StatusPublished_SetsPublishedAtNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(status: PostStatus.Published));
        }
        var after = DateTime.UtcNow.AddSeconds(1);

        dto.Status.Should().Be(PostStatus.Published);
        dto.PublishedAt.Should().NotBeNull();
        dto.PublishedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreateAsync_StatusDraft_PublishedAtNull_AndAuditsCreate()
    {
        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest());
        }

        dto.PublishedAt.Should().BeNull();

        await using var verify = _factory.Create(_connStr);
        var audit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(BlogPost)
                                   && a.Action == AuditAction.Create
                                   && a.EntityId == dto.Id.ToString());
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(7);
    }

    [Fact]
    public async Task UpdateAsync_ChangeSlugToExistingValue_AppendsSuffix()
    {
        PostDto a;
        PostDto b;
        await using (var ctx = _factory.Create(_connStr))
        {
            a = await CreateService(ctx).CreateAsync(NewRequest(title: "Alpha"));
            b = await CreateService(ctx).CreateAsync(NewRequest(title: "Beta"));
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            var updated = await CreateService(ctx).UpdateAsync(new UpdatePostRequest(
                b.Id, "Beta Updated", "alpha", null, "x", PostStatus.Draft, null, null, [], []));
            updated.Slug.Should().Be("alpha-2");
        }
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndAuditsDelete()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            var d = await CreateService(ctx).CreateAsync(NewRequest());
            id = d.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            (await CreateService(ctx).DeleteAsync(id)).Should().BeTrue();
        }

        await using var verify = _factory.Create(_connStr);

        var defaultQuery = await verify.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id);
        defaultQuery.Should().BeNull("soft-delete query filter ile gizlenmeli");

        var raw = await verify.Set<BlogPost>().IgnoreQueryFilters().FirstAsync(p => p.Id == id);
        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();

        var audit = await verify.Set<AuditEntry>()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(BlogPost)
                                   && a.Action == AuditAction.Delete
                                   && a.EntityId == id.ToString());
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task RestoreAsync_RestoresSoftDeleted_AuditsRestore()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            id = (await CreateService(ctx).CreateAsync(NewRequest())).Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).DeleteAsync(id);
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            (await CreateService(ctx).RestoreAsync(id)).Should().BeTrue();
        }

        await using var verify = _factory.Create(_connStr);
        var entity = await verify.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id);
        entity.Should().NotBeNull();
        entity!.IsDeleted.Should().BeFalse();

        var actions = await verify.Set<AuditEntry>()
            .Where(a => a.EntityName == nameof(BlogPost) && a.EntityId == id.ToString())
            .OrderBy(a => a.Timestamp)
            .Select(a => a.Action)
            .ToListAsync();
        actions.Should().Contain(AuditAction.Restore);
    }

    [Fact]
    public async Task PublishAsync_DraftToPublished_SetsPublishedAt()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            id = (await CreateService(ctx).CreateAsync(NewRequest())).Id;
        }
        PostDto published;
        await using (var ctx = _factory.Create(_connStr))
        {
            published = await CreateService(ctx).PublishAsync(id);
        }

        published.Status.Should().Be(PostStatus.Published);
        published.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UnpublishAsync_KeepsPublishedAt()
    {
        int id;
        DateTime? originalPublishedAt;
        // DB round-trip sonrasi datetime(6) precision'i ile karsilastiralim — in-memory DTO daha
        // hassas (ticks), MySQL microsec'e yuvarlar. Bu yuzden Create sonrasi DB'den okuyup
        // baseline aliyoruz.
        await using (var ctx = _factory.Create(_connStr))
        {
            var d = await CreateService(ctx).CreateAsync(NewRequest(status: PostStatus.Published));
            id = d.Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            var fresh = await CreateService(ctx).GetAsync(id);
            originalPublishedAt = fresh!.PublishedAt;
        }

        await Task.Delay(20);
        PostDto unpub;
        await using (var ctx = _factory.Create(_connStr))
        {
            unpub = await CreateService(ctx).UnpublishAsync(id);
        }

        unpub.Status.Should().Be(PostStatus.Draft);
        unpub.PublishedAt.Should().Be(originalPublishedAt, "yayin gecmisi tarihi korunmali");
    }

    [Fact]
    public async Task ListAsync_ExcludesSoftDeleted_OrderedByIdDesc()
    {
        int firstId, secondId, thirdId;
        await using (var ctx = _factory.Create(_connStr))
        {
            firstId = (await CreateService(ctx).CreateAsync(NewRequest(title: "First"))).Id;
            secondId = (await CreateService(ctx).CreateAsync(NewRequest(title: "Second"))).Id;
            thirdId = (await CreateService(ctx).CreateAsync(NewRequest(title: "Third"))).Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).DeleteAsync(secondId);
        }

        await using var read = _factory.Create(_connStr);
        var list = await CreateService(read).ListAsync();
        list.Should().HaveCount(2);
        list[0].Id.Should().Be(thirdId);
        list[1].Id.Should().Be(firstId);
    }

    [Fact]
    public async Task CreateAsync_WithCategoryIds_LinksJunction()
    {
        int catA, catB;
        await using (var ctx = _factory.Create(_connStr))
        {
            var ts = new TagService(ctx);
            var cs = new CategoryService(ctx);
            catA = (await cs.CreateAsync(new CreateCategoryRequest("Spor", null, null, null))).Id;
            catB = (await cs.CreateAsync(new CreateCategoryRequest("Futbol", null, null, catA))).Id;
        }

        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(categoryIds: new[] { catA, catB }));
        }

        dto.CategoryIds.Should().BeEquivalentTo(new[] { catA, catB });

        await using var verify = _factory.Create(_connStr);
        var links = await verify.Set<Cms.Modules.Blog.Domain.BlogPostCategory>()
            .Where(pc => pc.PostId == dto.Id).ToListAsync();
        links.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_WithTagNames_AutoCreatesTagsAndJunction()
    {
        PostDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await CreateService(ctx).CreateAsync(NewRequest(tagNames: new[] { "dotnet", "MySQL", "Şahane" }));
        }

        dto.TagNames.Should().HaveCount(3);

        await using var verify = _factory.Create(_connStr);
        var tags = await verify.Set<Cms.Modules.Blog.Domain.BlogTag>().ToListAsync();
        tags.Should().HaveCount(3);
        tags.Select(t => t.Slug).Should().Contain(new[] { "dotnet", "mysql", "sahane" });
    }

    [Fact]
    public async Task CreateAsync_WithExistingTags_ReusesThemBySlug()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await new TagService(ctx).CreateAsync(new CreateTagRequest("dotnet", null));
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).CreateAsync(NewRequest(tagNames: new[] { "dotnet", "csharp" }));
        }

        await using var verify = _factory.Create(_connStr);
        var totalTags = await verify.Set<Cms.Modules.Blog.Domain.BlogTag>().CountAsync();
        totalTags.Should().Be(2, "dotnet zaten vardi, sadece csharp yarat");
    }

    [Fact]
    public async Task UpdateAsync_RemovesUnlistedCategoriesAndTags()
    {
        int catA, catB;
        await using (var ctx = _factory.Create(_connStr))
        {
            var cs = new CategoryService(ctx);
            catA = (await cs.CreateAsync(new CreateCategoryRequest("CatA", null, null, null))).Id;
            catB = (await cs.CreateAsync(new CreateCategoryRequest("CatB", null, null, null))).Id;
        }

        PostDto created;
        await using (var ctx = _factory.Create(_connStr))
        {
            created = await CreateService(ctx).CreateAsync(NewRequest(
                title: "T",
                categoryIds: new[] { catA, catB },
                tagNames: new[] { "x", "y", "z" }));
        }

        await using (var ctx = _factory.Create(_connStr))
        {
            await CreateService(ctx).UpdateAsync(new UpdatePostRequest(
                created.Id, "T", created.Slug, null, "body", PostStatus.Draft, null, null,
                new[] { catA }, new[] { "x" }));
        }

        await using var verify = _factory.Create(_connStr);
        var cats = await verify.Set<Cms.Modules.Blog.Domain.BlogPostCategory>()
            .Where(pc => pc.PostId == created.Id).Select(pc => pc.CategoryId).ToListAsync();
        var tagLinks = await verify.Set<Cms.Modules.Blog.Domain.BlogPostTag>()
            .Where(pt => pt.PostId == created.Id).CountAsync();

        cats.Should().BeEquivalentTo(new[] { catA });
        tagLinks.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_InvalidCategoryId_Throws()
    {
        await using var ctx = _factory.Create(_connStr);
        var act = async () => await CreateService(ctx).CreateAsync(NewRequest(categoryIds: new[] { 9999 }));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*9999*");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsPostOrNull()
    {
        PostDto created;
        await using (var ctx = _factory.Create(_connStr))
        {
            created = await CreateService(ctx).CreateAsync(NewRequest(title: "Find Me"));
        }

        await using var read = _factory.Create(_connStr);
        var found = await CreateService(read).GetBySlugAsync(created.Slug);
        var missing = await CreateService(read).GetBySlugAsync("does-not-exist");

        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);
        missing.Should().BeNull();
    }

    private sealed class StubMediaService : IMediaService
    {
        private readonly Dictionary<int, MediaFile> _store = [];

        public void Seed(MediaFile file) => _store[file.Id] = file;

        public Task<MediaFile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<MediaFile?>(_store.TryGetValue(id, out var f) ? f : null);

        public Task<MediaFile> UploadAsync(Stream content, string fileName, string mimeType, string? altText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Stream?> OpenContentAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<MediaFile>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MediaFile>>([.. _store.Values]);

        public Task UpdateMetadataAsync(int id, string? altText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
