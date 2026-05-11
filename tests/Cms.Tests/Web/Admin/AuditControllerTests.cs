namespace Cms.Tests.Web.Admin;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using Cms.Core.Tenancy;
using Cms.Tests.Infrastructure;
using Cms.Web.Areas.Admin.Controllers;
using Cms.Web.Areas.Admin.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection(MySqlCollection.Name)]
public class AuditControllerTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;
    private Tenant _tenant = null!;
    private TenantDbContextFactory _factory = null!;
    private StubProvisioningService _provisioning = null!;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("auditui");

        _tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = "auditui",
            DisplayName = "Audit UI Test",
            ConnectionString = _connStr,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _factory = new TenantDbContextFactory(new List<ModuleDescriptor>());

        await using (var ctx = _factory.Create(_connStr))
        {
            await ctx.Database.EnsureCreatedAsync();

            var baseTime = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);
            ctx.Set<AuditEntry>().AddRange(
                new AuditEntry
                {
                    EntityName = "Post",
                    EntityId = "1",
                    Action = AuditAction.Create,
                    UserId = 1,
                    Timestamp = baseTime.AddMinutes(-10),
                    Changes = null,
                },
                new AuditEntry
                {
                    EntityName = "Comment",
                    EntityId = "2",
                    Action = AuditAction.Update,
                    UserId = 2,
                    Timestamp = baseTime.AddMinutes(-5),
                    Changes = "{\"name\":{\"old\":\"a\",\"new\":\"b\"}}",
                },
                new AuditEntry
                {
                    EntityName = "Post",
                    EntityId = "3",
                    Action = AuditAction.Delete,
                    UserId = 1,
                    Timestamp = baseTime,
                    Changes = null,
                });
            await ctx.SaveChangesAsync();
        }

        _provisioning = new StubProvisioningService(_tenant);
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    private AuditController BuildController()
    {
        var controller = new AuditController(_provisioning, _factory);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    [Fact]
    public async Task Index_NoTenantSelected_ReturnsViewWithTenantList_AndEmptyEntries()
    {
        var result = await BuildController().Index(null, null, null, null, null, null, 1);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AuditIndexViewModel>().Subject;
        model.Tenants.Should().HaveCount(1);
        model.SelectedTenantId.Should().BeNull();
        model.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Index_TenantSelected_ReturnsAuditEntries_OrderedByTimestampDesc()
    {
        var result = await BuildController().Index(_tenant.Id, null, null, null, null, null, 1);

        var model = ((ViewResult)result).Model.Should().BeOfType<AuditIndexViewModel>().Subject;
        model.SelectedTenantId.Should().Be(_tenant.Id);
        model.TotalCount.Should().Be(3);
        model.Entries.Should().HaveCount(3);
        model.Entries[0].EntityName.Should().Be("Post");
        model.Entries[0].Action.Should().Be(AuditAction.Delete); // en yeni
        model.Entries[^1].Action.Should().Be(AuditAction.Create); // en eski
    }

    [Fact]
    public async Task Index_FilterByEntityName_ReturnsFilteredResults()
    {
        var result = await BuildController().Index(_tenant.Id, "Post", null, null, null, null, 1);

        var model = ((ViewResult)result).Model.Should().BeOfType<AuditIndexViewModel>().Subject;
        model.Entries.Should().HaveCount(2);
        model.Entries.Should().OnlyContain(e => e.EntityName == "Post");
    }

    [Fact]
    public async Task Index_FilterByAction_ReturnsFilteredResults()
    {
        var result = await BuildController().Index(_tenant.Id, null, null, auditAction: AuditAction.Update, null, null, 1);

        var model = ((ViewResult)result).Model.Should().BeOfType<AuditIndexViewModel>().Subject;
        model.Entries.Should().HaveCount(1);
        model.Entries[0].Action.Should().Be(AuditAction.Update);
        model.Entries[0].EntityName.Should().Be("Comment");
    }

    [Fact]
    public async Task Index_UnknownTenantId_AddsModelError_AndClearsSelection()
    {
        var result = await BuildController().Index(Guid.NewGuid(), null, null, null, null, null, 1);

        var view = (ViewResult)result;
        var model = view.Model.Should().BeOfType<AuditIndexViewModel>().Subject;
        model.SelectedTenantId.Should().BeNull();
        view.ViewData.ModelState.IsValid.Should().BeFalse();
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
}
