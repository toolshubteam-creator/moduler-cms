namespace Cms.Core.Authorization;

using System.Globalization;
using System.Security.Claims;
using Cms.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;

public sealed class HasPermissionHandler(
    IPermissionService permissionService,
    ITenantContext tenantContext) : AuthorizationHandler<HasPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            return;
        }

        var tenantId = tenantContext.IsResolved ? tenantContext.Current!.Id : (Guid?)null;

        var allowed = await permissionService.HasPermissionAsync(userId, tenantId, requirement.PermissionKey);
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
