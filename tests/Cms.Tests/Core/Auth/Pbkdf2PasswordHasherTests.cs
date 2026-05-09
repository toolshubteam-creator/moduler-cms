namespace Cms.Tests.Core.Auth;

using Cms.Core.Auth;
using FluentAssertions;
using Xunit;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _sut = new();

    [Fact]
    public void Hash_GivenPassword_ReturnsNonEmptyEncodedString()
    {
        var hash = _sut.Hash("Sakarya123!");
        hash.Should().NotBeNullOrEmpty();
        hash.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashesDueToSalt()
    {
        var h1 = _sut.Hash("Sakarya123!");
        var h2 = _sut.Hash("Sakarya123!");
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("Sakarya123!");
        _sut.Verify("Sakarya123!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("Sakarya123!");
        _sut.Verify("YanlisSifre!", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        _sut.Verify("Sakarya123!", "not.a.valid.hash.format").Should().BeFalse();
        _sut.Verify("Sakarya123!", "garbage").Should().BeFalse();
        _sut.Verify("Sakarya123!", "").Should().BeFalse();
    }

    [Fact]
    public void Hash_EmptyPassword_Throws()
    {
        var act = () => _sut.Hash("");
        act.Should().Throw<ArgumentException>();
    }
}
