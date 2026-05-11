namespace Cms.Tests.Auditing.Fixtures;

using Cms.Core.Domain.Auditing;

public sealed class TestSoftDeletableEntity : IAuditable, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
