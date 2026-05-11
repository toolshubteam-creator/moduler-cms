namespace Cms.Tests.Web.Admin;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Data.Interceptors;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Security;
using Cms.Core.Tenancy;
using Cms.Tests.Auditing.Fixtures;
using Cms.Tests.Infrastructure;
using Cms.Tests.Security;
using Cms.Web.Areas.Admin.Controllers;
using Cms.Web.Areas.Admin.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection(MySqlCollection.Name)]
public class SoftDeleteControllerTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly FakeCurrentUserService _user = new() { UserId = 1 };
    private string _connStr = string.Empty;
    private Tenant _tenant = null!;
    private TenantDbContextFactory _factory = null!;
    private ModuleDescriptorRegistry _registry = null!;
    private StubProvisioningService _provisioning = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("softdelui");

        _tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = "softdelui",
            DisplayName = "Soft Delete UI Test",
            ConnectionString = _connStr,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        var sp = services.BuildServiceProvider();

        var moduleAssembly = typeof(TestAuditModule).Assembly;
        var modules = new List<ModuleDescriptor>
        {
            new()
            {
                Instance = new TestAuditModule(),
                Assembly = moduleAssembly,
                DllPath = moduleAssembly.Location,
            },
        };
        var interceptors = new IInterceptor[]
        {
            new SoftDeleteInterceptor(),
            new AuditSaveChangesInterceptor(sp),
        };
        _registry = new ModuleDescriptorRegistry();
        _factory = new TenantDbContextFactory(modules, interceptors, _registry);

        await using (var ctx = _factory.Create(_connStr))
        {
            await ctx.Database.EnsureCreatedAsync();

            // Iki TestSoftDeletableEntity ekle, birini soft-delete et
            var keep = new TestSoftDeletableEntity { Name = "keep" };
            var del = new TestSoftDeletableEntity { Name = "to-restore" };
            ctx.Set<TestSoftDeletableEntity>().AddRange(keep, del);
            await ctx.SaveChangesAsync();

            ctx.Set<TestSoftDeletableEntity>().Remove(del);
            await ctx.SaveChangesAsync();
        }

        _provisioning = new StubProvisioningService(_tenant);
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private SoftDeleteController BuildController()
    {
        var controller = new SoftDeleteController(_provisioning, _factory, _registry);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.ControllerContext.HttpContext,
            new StubTempDataProvider());
        return controller;
    }

    [Fact]
    public async Task Index_NoTenantOrEntitySelected_ReturnsViewWithRegisteredEntityOptions()
    {
        var result = await BuildController().Index(null, null, 1);

        var model = ((ViewResult)result).Model.Should().BeOfType<SoftDeleteIndexViewModel>().Subject;
        model.EntityTypeOptions.Should().Contain(nameof(TestSoftDeletableEntity));
        model.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Index_TenantAndEntitySelected_ReturnsOnlyDeletedRows()
    {
        var result = await BuildController().Index(_tenant.Id, nameof(TestSoftDeletableEntity), 1);

        var model = ((ViewResult)result).Model.Should().BeOfType<SoftDeleteIndexViewModel>().Subject;
        model.SelectedTenantId.Should().Be(_tenant.Id);
        model.TotalCount.Should().Be(1);
        model.Entries.Should().HaveCount(1);
        model.Entries[0].Display.Should().Be("to-restore");
    }

    [Fact]
    public async Task Restore_ValidEntity_SetsIsDeletedFalse_AndAuditsRestoreAction()
    {
        // Silinmis entity'nin id'sini bul
        Guid deletedId;
        await using (var lookup = _factory.Create(_connStr))
        {
            var row = await lookup.Set<TestSoftDeletableEntity>()
                .IgnoreQueryFilters()
                .FirstAsync(e => e.IsDeleted);
            deletedId = row.Id;
        }

        var result = await BuildController().Restore(_tenant.Id, nameof(TestSoftDeletableEntity), deletedId.ToString());

        result.Should().BeOfType<RedirectToActionResult>();

        // DB tarafinda IsDeleted=false olmali, audit Restore action yazilmali
        await using var verify = _factory.Create(_connStr);
        var entity = await verify.Set<TestSoftDeletableEntity>().FirstOrDefaultAsync(e => e.Id == deletedId);
        entity.Should().NotBeNull("restore sonrasi default query'de gozukmeli");
        entity!.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();

        var actions = await verify.Set<AuditEntry>()
            .Where(a => a.EntityId == deletedId.ToString())
            .Select(a => a.Action)
            .ToListAsync();
        actions.Should().Contain(AuditAction.Restore);
    }

    private sealed class StubProvisioningService(Tenant tenant) : ITenantProvisioningService
    {
        public Task<TenantProvisioningResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([tenant]);

        public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(tenantId == tenant.Id ? tenant : null);

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        private readonly Dictionary<string, object> _store = [];

        public IDictionary<string, object> LoadTempData(HttpContext context) => _store;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _store.Clear();
            foreach (var kv in values)
            {
                _store[kv.Key] = kv.Value;
            }
        }
    }
}
