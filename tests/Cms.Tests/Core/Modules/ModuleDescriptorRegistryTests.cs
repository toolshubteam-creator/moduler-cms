namespace Cms.Tests.Core.Modules;

using Cms.Core.Domain.Auditing;
using Cms.Core.Modules;
using FluentAssertions;
using Xunit;

public class ModuleDescriptorRegistryTests
{
    [Fact]
    public void RegisterSoftDeletableTypes_GetterReturnsRegisteredTypes()
    {
        var registry = new ModuleDescriptorRegistry();
        registry.RegisterSoftDeletableTypes([typeof(SampleSoftDeletable)]);

        var registered = registry.GetSoftDeletableEntityTypes();

        registered.Should().Contain(typeof(SampleSoftDeletable));
    }

    [Fact]
    public void RegisterSoftDeletableTypes_DoesNotDuplicateOnRepeatRegistration()
    {
        var registry = new ModuleDescriptorRegistry();
        registry.RegisterSoftDeletableTypes([typeof(SampleSoftDeletable)]);
        registry.RegisterSoftDeletableTypes([typeof(SampleSoftDeletable)]);

        registry.GetSoftDeletableEntityTypes().Should().HaveCount(1);
    }

    private sealed class SampleSoftDeletable : ISoftDeletable
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
