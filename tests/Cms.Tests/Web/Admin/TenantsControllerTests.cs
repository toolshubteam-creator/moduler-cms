namespace Cms.Tests.Web.Admin;

using Cms.Core.Data.Entities;
using Cms.Core.Tenancy;
using Cms.Web.Areas.Admin.Controllers;
using Cms.Web.Models.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

public class TenantsControllerTests
{
    private sealed class StubProvisioningService : ITenantProvisioningService
    {
        public Func<CreateTenantRequest, TenantProvisioningResult> CreateBehavior { get; set; } = _ => throw new NotImplementedException();

        public List<Tenant> Tenants { get; } = [];

        public Task<TenantProvisioningResult> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
            => Task.FromResult(CreateBehavior(request));

        public Task<bool> DeactivateAsync(Guid tenantId, CancellationToken ct = default)
        {
            var t = Tenants.FirstOrDefault(x => x.Id == tenantId);
            if (t is null)
            {
                return Task.FromResult(false);
            }

            t.IsActive = false;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(
                includeInactive ? Tenants : Tenants.Where(t => t.IsActive).ToList());

        public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Tenants.FirstOrDefault(t => t.Id == tenantId));

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Tenants.FirstOrDefault(t => t.Slug == slug));
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static TenantsController CreateSut(ITenantProvisioningService svc)
    {
        var ctx = new DefaultHttpContext();
        var controller = new TenantsController(svc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ctx,
            },
            TempData = new TempDataDictionary(ctx, new StubTempDataProvider()),
        };
        return controller;
    }

    [Fact]
    public async Task Index_DefaultParameters_ReturnsActiveTenants()
    {
        var svc = new StubProvisioningService();
        svc.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Slug = "active", DisplayName = "A", IsActive = true, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow });
        svc.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Slug = "inactive", DisplayName = "I", IsActive = false, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow });
        var sut = CreateSut(svc);

        var result = await sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeAssignableTo<IReadOnlyList<Tenant>>().Subject;
        model.Should().HaveCount(1);
        model[0].Slug.Should().Be("active");
    }

    [Fact]
    public async Task Index_IncludeInactive_ReturnsAll()
    {
        var svc = new StubProvisioningService();
        svc.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Slug = "active", DisplayName = "A", IsActive = true, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow });
        svc.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Slug = "inactive", DisplayName = "I", IsActive = false, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow });
        var sut = CreateSut(svc);

        var result = await sut.Index(includeInactive: true);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeAssignableTo<IReadOnlyList<Tenant>>().Subject;
        model.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Post_ValidModel_RedirectsToIndex()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Slug = "acme", DisplayName = "Acme", IsActive = true, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow };
        var svc = new StubProvisioningService { CreateBehavior = _ => TenantProvisioningResult.Success(tenant) };
        var sut = CreateSut(svc);

        var result = await sut.Create(new CreateTenantViewModel { Slug = "acme", DisplayName = "Acme" });

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
    }

    [Fact]
    public async Task Create_Post_ServiceFailure_ReturnsViewWithError()
    {
        var svc = new StubProvisioningService
        {
            CreateBehavior = _ => TenantProvisioningResult.SlugAlreadyExists("acme"),
        };
        var sut = CreateSut(svc);

        var result = await sut.Create(new CreateTenantViewModel { Slug = "acme", DisplayName = "Acme" });

        result.Should().BeOfType<ViewResult>();
        sut.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithoutCallingService()
    {
        var svc = new StubProvisioningService { CreateBehavior = _ => throw new InvalidOperationException("Service should not be called") };
        var sut = CreateSut(svc);
        sut.ModelState.AddModelError("Slug", "Required");

        var result = await sut.Create(new CreateTenantViewModel());

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Deactivate_ExistingTenant_RedirectsToIndex()
    {
        var svc = new StubProvisioningService();
        var id = Guid.NewGuid();
        svc.Tenants.Add(new Tenant { Id = id, Slug = "x", DisplayName = "X", IsActive = true, ConnectionString = "x", CreatedAtUtc = DateTime.UtcNow });
        var sut = CreateSut(svc);

        var result = await sut.Deactivate(id);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
    }
}
