namespace Cms.Web.Security;

using System.Globalization;
using System.Security.Claims;
using Cms.Core.Security;

public sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int? UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var raw = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public string? Email
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            return principal?.FindFirst(ClaimTypes.Email)?.Value
                ?? principal?.FindFirst(ClaimTypes.Name)?.Value;
        }
    }
}
