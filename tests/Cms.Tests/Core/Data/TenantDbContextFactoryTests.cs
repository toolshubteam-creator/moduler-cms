namespace Cms.Tests.Core.Data;

using Cms.Core.Data;
using FluentAssertions;
using Xunit;

public class TenantDbContextFactoryTests
{
    [Fact]
    public void Create_NullConnectionString_Throws()
    {
        var factory = new TenantDbContextFactory([]);

        var act = () => factory.Create(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyConnectionString_Throws()
    {
        var factory = new TenantDbContextFactory([]);

        var act = () => factory.Create("   ");

        act.Should().Throw<ArgumentException>();
    }
}
