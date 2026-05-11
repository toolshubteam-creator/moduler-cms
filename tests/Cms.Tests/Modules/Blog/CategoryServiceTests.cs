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
public class CategoryServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 7 };
    private string _connStr = string.Empty;
    private TenantDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("blogcat");

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

    private CategoryService NewService(TenantDbContext ctx) => new(ctx);

    [Fact]
    public async Task CreateAsync_AutoGeneratesSlugFromName_TurkishNormalized()
    {
        CategoryDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await NewService(ctx).CreateAsync(new CreateCategoryRequest("Şuçuk İçi Spor", null, null, null));
        }
        dto.Slug.Should().Be("sucuk-ici-spor");
    }

    [Fact]
    public async Task CreateAsync_ProvidedSlug_IsUsedAndNormalized()
    {
        CategoryDto dto;
        await using (var ctx = _factory.Create(_connStr))
        {
            dto = await NewService(ctx).CreateAsync(new CreateCategoryRequest("Whatever", "Special Slug!!", null, null));
        }
        dto.Slug.Should().Be("special-slug");
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_AppendsSuffix()
    {
        await using (var ctx = _factory.Create(_connStr))
        {
            await NewService(ctx).CreateAsync(new CreateCategoryRequest("Spor", null, null, null));
        }
        CategoryDto second;
        await using (var ctx = _factory.Create(_connStr))
        {
            second = await NewService(ctx).CreateAsync(new CreateCategoryRequest("Spor", null, null, null));
        }
        second.Slug.Should().Be("spor-2");
    }

    [Fact]
    public async Task CreateAsync_InvalidParentId_Throws()
    {
        await using var ctx = _factory.Create(_connStr);
        var act = async () => await NewService(ctx).CreateAsync(new CreateCategoryRequest("Futbol", null, null, 999));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*999*");
    }

    [Fact]
    public async Task CreateAsync_ValidParent_LinksHierarchy()
    {
        CategoryDto parent;
        await using (var ctx = _factory.Create(_connStr))
        {
            parent = await NewService(ctx).CreateAsync(new CreateCategoryRequest("Spor", null, null, null));
        }
        CategoryDto child;
        await using (var ctx = _factory.Create(_connStr))
        {
            child = await NewService(ctx).CreateAsync(new CreateCategoryRequest("Futbol", null, null, parent.Id));
        }
        child.ParentCategoryId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task UpdateAsync_SetSelfAsParent_Throws()
    {
        CategoryDto cat;
        await using (var ctx = _factory.Create(_connStr))
        {
            cat = await NewService(ctx).CreateAsync(new CreateCategoryRequest("X", null, null, null));
        }
        await using var ctx2 = _factory.Create(_connStr);
        var act = async () => await NewService(ctx2).UpdateAsync(
            new UpdateCategoryRequest(cat.Id, "X", cat.Slug, null, cat.Id));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*kendi parent*");
    }

    [Fact]
    public async Task UpdateAsync_CycleDetection_Throws()
    {
        // A → B → C; sonra A.Parent=C → cycle
        int a, b, c;
        await using (var ctx = _factory.Create(_connStr))
        {
            var svc = NewService(ctx);
            a = (await svc.CreateAsync(new CreateCategoryRequest("A", null, null, null))).Id;
            b = (await svc.CreateAsync(new CreateCategoryRequest("B", null, null, a))).Id;
            c = (await svc.CreateAsync(new CreateCategoryRequest("C", null, null, b))).Id;
        }

        await using var ctx2 = _factory.Create(_connStr);
        var act = async () => await NewService(ctx2).UpdateAsync(
            new UpdateCategoryRequest(a, "A", "a", null, c));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*dongusu*");
    }

    [Fact]
    public async Task UpdateAsync_ChangeSlugToExisting_AppendsSuffix()
    {
        int a, b;
        await using (var ctx = _factory.Create(_connStr))
        {
            var svc = NewService(ctx);
            a = (await svc.CreateAsync(new CreateCategoryRequest("Alpha", null, null, null))).Id;
            b = (await svc.CreateAsync(new CreateCategoryRequest("Beta", null, null, null))).Id;
        }

        await using var ctx2 = _factory.Create(_connStr);
        var updated = await NewService(ctx2).UpdateAsync(
            new UpdateCategoryRequest(b, "Beta", "alpha", null, null));
        updated.Slug.Should().Be("alpha-2");
    }

    [Fact]
    public async Task DeleteAsync_HasChildren_Throws()
    {
        int parent, child;
        await using (var ctx = _factory.Create(_connStr))
        {
            var svc = NewService(ctx);
            parent = (await svc.CreateAsync(new CreateCategoryRequest("P", null, null, null))).Id;
            child = (await svc.CreateAsync(new CreateCategoryRequest("C", null, null, parent))).Id;
        }

        await using var ctx2 = _factory.Create(_connStr);
        var act = async () => await NewService(ctx2).DeleteAsync(parent);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*alt kategori*");
        _ = child; // referans tutmak icin
    }

    [Fact]
    public async Task DeleteAsync_NoChildren_SoftDeletes()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            id = (await NewService(ctx).CreateAsync(new CreateCategoryRequest("Solo", null, null, null))).Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await NewService(ctx).DeleteAsync(id);
        }

        await using var verify = _factory.Create(_connStr);
        var visible = await verify.Set<BlogCategory>().FirstOrDefaultAsync(c => c.Id == id);
        var raw = await verify.Set<BlogCategory>().IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        visible.Should().BeNull("soft-delete query filter ile gizlenmeli");
        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RestoreAsync_RestoresSoftDeleted()
    {
        int id;
        await using (var ctx = _factory.Create(_connStr))
        {
            id = (await NewService(ctx).CreateAsync(new CreateCategoryRequest("Tmp", null, null, null))).Id;
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await NewService(ctx).DeleteAsync(id);
        }
        await using (var ctx = _factory.Create(_connStr))
        {
            await NewService(ctx).RestoreAsync(id);
        }

        await using var verify = _factory.Create(_connStr);
        var entity = await verify.Set<BlogCategory>().FirstOrDefaultAsync(c => c.Id == id);
        entity.Should().NotBeNull();
        entity!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task ListWithIndentAsync_ComputesDepthCorrectly()
    {
        int p, ch, gc;
        await using (var ctx = _factory.Create(_connStr))
        {
            var svc = NewService(ctx);
            p = (await svc.CreateAsync(new CreateCategoryRequest("Spor", null, null, null))).Id;
            ch = (await svc.CreateAsync(new CreateCategoryRequest("Futbol", null, null, p))).Id;
            gc = (await svc.CreateAsync(new CreateCategoryRequest("Super Lig", null, null, ch))).Id;
        }

        await using var ctx2 = _factory.Create(_connStr);
        var tree = await NewService(ctx2).ListWithIndentAsync();
        tree.Should().HaveCount(3);
        tree.Single(n => n.Id == p).Depth.Should().Be(0);
        tree.Single(n => n.Id == ch).Depth.Should().Be(1);
        tree.Single(n => n.Id == gc).Depth.Should().Be(2);
    }
}
