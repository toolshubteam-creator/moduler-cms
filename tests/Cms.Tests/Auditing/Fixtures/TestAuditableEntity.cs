namespace Cms.Tests.Auditing.Fixtures;

using Cms.Core.Domain.Auditing;

public sealed class TestAuditableEntity : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    [AuditIgnore]
    public string? SecretField { get; set; }
}
