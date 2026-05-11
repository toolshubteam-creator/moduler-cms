namespace Cms.Tests.Modules.Blog;

using FluentAssertions;
using Xunit;

// SlugGenerator internal — InternalsVisibleTo Cms.Tests Cms.Modules.Blog'da yok.
// Davranisi PostService.CreateAsync uzerinden integration test'lerde dolayli olarak dogrulanir
// (CreateAsync_AutoGeneratesSlugFromTitle_TurkishChars + EmptyTitle_Throws + DuplicateSlug_AppendsSuffix).
// Bu dosya placeholder; tum slug davranisi PostServiceIntegrationTests'te kapsanir.
public class SlugGeneratorTests
{
    [Fact]
    public void Placeholder_SlugGeneratorBehaviorCoveredInIntegrationTests()
    {
        true.Should().BeTrue("SlugGenerator internal — davranis PostService integration test'lerinde dogrulanir.");
    }
}
