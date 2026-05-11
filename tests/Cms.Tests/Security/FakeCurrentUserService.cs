namespace Cms.Tests.Security;

using Cms.Core.Security;

public sealed class FakeCurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }

    public string? Email { get; set; }
}
