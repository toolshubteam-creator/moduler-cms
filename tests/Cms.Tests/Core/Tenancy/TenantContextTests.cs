namespace Cms.Tests.Core.Tenancy;

using Cms.Core.Data.Entities;
using Cms.Core.Tenancy;
using FluentAssertions;
using Xunit;

public class TenantContextTests
{
    private static Tenant NewTenant(string slug) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        DisplayName = slug,
        ConnectionString = "x",
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public void IsResolved_BeforeSet_IsFalse()
    {
        ITenantContext sut = new TenantContext();

        sut.IsResolved.Should().BeFalse();
        sut.Current.Should().BeNull();
    }

    [Fact]
    public void Set_FirstTime_StoresTenant()
    {
        ITenantContext sut = new TenantContext();
        var tenant = NewTenant("acme");

        sut.Set(tenant);

        sut.IsResolved.Should().BeTrue();
        sut.Current.Should().Be(tenant);
    }

    [Fact]
    public void Set_Twice_Throws()
    {
        ITenantContext sut = new TenantContext();
        sut.Set(NewTenant("a"));

        var act = () => sut.Set(NewTenant("b"));

        act.Should().Throw<InvalidOperationException>();
    }
}
