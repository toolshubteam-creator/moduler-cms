namespace Cms.Tests.Core.Tenancy;

using Cms.Core.Tenancy;
using FluentAssertions;
using Xunit;

public class SlugValidatorTests
{
    private static readonly IReadOnlyList<string> Reserved = ["admin", "account", "test"];

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a1b2")]
    [InlineData("contoso-2026")]
    public void Validate_ValidSlug_ReturnsValid(string slug)
    {
        var result = SlugValidator.Validate(slug, Reserved);

        result.IsValid.Should().BeTrue();
        result.Normalized.Should().Be(slug.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("1acme")]
    [InlineData("acme!")]
    [InlineData("acme_corp")]
    [InlineData("acme corp")]
    [InlineData("very-long-slug-that-exceeds-thirty-one-characters-limit")]
    public void Validate_InvalidFormat_ReturnsInvalid(string slug)
    {
        var result = SlugValidator.Validate(slug, Reserved);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("test")]
    public void Validate_ReservedSlug_ReturnsInvalid(string slug)
    {
        var result = SlugValidator.Validate(slug, Reserved);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("rezerve");
    }

    [Fact]
    public void Validate_TrimsWhitespace()
    {
        var result = SlugValidator.Validate("  acme  ", Reserved);

        result.IsValid.Should().BeTrue();
        result.Normalized.Should().Be("acme");
    }

    [Fact]
    public void Validate_NormalizesToLowerInvariant()
    {
        var result = SlugValidator.Validate("Acme-Corp", Reserved);

        result.IsValid.Should().BeTrue();
        result.Normalized.Should().Be("acme-corp");
    }
}
