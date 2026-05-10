namespace Cms.Core.Authorization;

using System.Globalization;
using System.Security.Claims;
using Cms.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public sealed class SystemRoleHandler(MasterDbContext db) : AuthorizationHandler<SystemRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SystemRoleRequirement requirement)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            return;
        }

        var hasSystemRole = await db.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId && ur.Role.IsSystem);

        if (hasSystemRole)
        {
            context.Succeed(requirement);
        }
    }
}
