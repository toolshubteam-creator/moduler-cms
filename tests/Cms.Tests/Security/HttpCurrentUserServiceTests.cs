namespace Cms.Tests.Security;

using System.Security.Claims;
using Cms.Web.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

public class HttpCurrentUserServiceTests
{
    [Fact]
    public void UserId_AuthenticatedWithIntegerClaim_ReturnsParsedInt()
    {
        var accessor = BuildAccessor(new Claim(ClaimTypes.NameIdentifier, "123"));
        var sut = new HttpCurrentUserService(accessor);

        sut.UserId.Should().Be(123);
    }

    [Fact]
    public void UserId_NoHttpContext_ReturnsNull()
    {
        var accessor = new HttpContextAccessor();
        var sut = new HttpCurrentUserService(accessor);

        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_InvalidIntegerClaim_ReturnsNull()
    {
        var accessor = BuildAccessor(new Claim(ClaimTypes.NameIdentifier, "not-an-int"));
        var sut = new HttpCurrentUserService(accessor);

        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_NoNameIdentifierClaim_ReturnsNull()
    {
        var accessor = BuildAccessor(new Claim(ClaimTypes.Email, "u@example.com"));
        var sut = new HttpCurrentUserService(accessor);

        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void Email_ReadsEmailClaim()
    {
        var accessor = BuildAccessor(
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Email, "user@example.com"));
        var sut = new HttpCurrentUserService(accessor);

        sut.Email.Should().Be("user@example.com");
    }

    [Fact]
    public void Email_FallsBackToNameClaim()
    {
        var accessor = BuildAccessor(
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "fallback-name"));
        var sut = new HttpCurrentUserService(accessor);

        sut.Email.Should().Be("fallback-name");
    }

    private static IHttpContextAccessor BuildAccessor(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = ctx };
    }
}
