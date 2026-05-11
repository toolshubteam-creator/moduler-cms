namespace Cms.Tests.Modules.Seo;

using Cms.Modules.Seo.Contracts;
using Cms.Modules.Seo.TagHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

public class SeoMetaTagHelperTests
{
    private static TagHelperContext NewContext() =>
        new(allAttributes: [], items: new Dictionary<object, object>(), uniqueId: "test");

    private static TagHelperOutput NewOutput() =>
        new("seo-meta",
            attributes: [],
            getChildContentAsync: (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    [Fact]
    public async Task ProcessAsync_AllFieldsResolved_RendersAllTags()
    {
        var stub = new StubSeoService(new SeoMetaResolved(
            "My Title", "My Desc", "https://cdn/og.jpg", "https://site/page", "index, follow"));
        var sut = new SeoMetaTagHelper(stub) { TargetType = "post", TargetId = "42" };
        var output = NewOutput();

        await sut.ProcessAsync(NewContext(), output);

        var html = output.Content.GetContent();
        html.Should().Contain("<title>My Title</title>");
        html.Should().Contain("<meta name=\"description\" content=\"My Desc\" />");
        html.Should().Contain("<meta property=\"og:image\" content=\"https://cdn/og.jpg\" />");
        html.Should().Contain("<link rel=\"canonical\" href=\"https://site/page\" />");
        html.Should().Contain("<meta name=\"robots\" content=\"index, follow\" />");
        output.TagName.Should().BeNull("wrapper tag yazilmamali");
    }

    [Fact]
    public async Task ProcessAsync_AllFieldsNull_RendersEmpty()
    {
        var stub = new StubSeoService(new SeoMetaResolved(null, null, null, null, null));
        var sut = new SeoMetaTagHelper(stub) { TargetType = "post", TargetId = "1" };
        var output = NewOutput();

        await sut.ProcessAsync(NewContext(), output);

        output.Content.GetContent().Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_TargetTypeOrIdMissing_RendersEmpty_WithoutResolveCall()
    {
        var stub = new StubSeoService(new SeoMetaResolved("X", null, null, null, null));
        var sut = new SeoMetaTagHelper(stub); // TargetType + TargetId empty
        var output = NewOutput();

        await sut.ProcessAsync(NewContext(), output);

        output.Content.GetContent().Should().BeEmpty();
        stub.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_TitleWithHtml_EncodesContent()
    {
        var stub = new StubSeoService(new SeoMetaResolved(
            "<script>alert('xss')</script>", "Desc", null, null, null));
        var sut = new SeoMetaTagHelper(stub) { TargetType = "post", TargetId = "99" };
        var output = NewOutput();

        await sut.ProcessAsync(NewContext(), output);

        var html = output.Content.GetContent();
        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().Contain("alert");
    }

    private sealed class StubSeoService(SeoMetaResolved resolved) : ISeoMetaService
    {
        public int CallCount { get; private set; }

        public Task<SeoMetaResolved> ResolveAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(resolved);
        }

        public Task<SeoMeta?> GetAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SeoMeta> SetAsync(string targetType, string targetId, SeoMetaInput input, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> DeleteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<SeoMeta>> ListAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
