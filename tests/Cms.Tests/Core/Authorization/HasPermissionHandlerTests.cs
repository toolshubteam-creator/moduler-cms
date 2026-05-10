namespace Cms.Tests.Core.Authorization;

using System.Globalization;
using System.Security.Claims;
using Cms.Core.Authorization;
using Cms.Core.Data.Entities;
using Cms.Core.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

public class HasPermissionHandlerTests
{
    private sealed class StubPermissionService(bool result, IReadOnlySet<string>? perms = null) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(int userId, Guid? tenantId, string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<IReadOnlySet<string>> GetUserPermissionsAsync(int userId, Guid? tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(perms ?? new HashSet<string>());
    }

    private sealed class StubTenantContext : ITenantContext
    {
        private Tenant? _t;

        public Tenant? Current => _t;

        public bool IsResolved => _t is not null;

        public void Set(Tenant tenant) => _t = tenant;
    }

    private static AuthorizationHandlerContext BuildContext(int? userId, HasPermissionRequirement requirement)
    {
        var claims = userId.HasValue
            ? new List<Claim> { new(ClaimTypes.NameIdentifier, userId.Value.ToString(CultureInfo.InvariantCulture)) }
            : [];
        var identity = new ClaimsIdentity(claims, userId.HasValue ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([requirement], principal, resource: null);
    }

    [Fact]
    public async Task Handle_PermissionGranted_Succeeds()
    {
        var req = new HasPermissionRequirement("blog.posts.create");
        var ctx = BuildContext(userId: 42, req);
        var sut = new HasPermissionHandler(new StubPermissionService(true), new StubTenantContext());

        await sut.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PermissionDenied_DoesNotSucceed()
    {
        var req = new HasPermissionRequirement("blog.posts.create");
        var ctx = BuildContext(userId: 42, req);
        var sut = new HasPermissionHandler(new StubPermissionService(false), new StubTenantContext());

        await sut.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoUserIdClaim_DoesNotSucceed()
    {
        var req = new HasPermissionRequirement("blog.posts.create");
        var ctx = BuildContext(userId: null, req);
        var sut = new HasPermissionHandler(new StubPermissionService(true), new StubTenantContext());

        await sut.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }
}
