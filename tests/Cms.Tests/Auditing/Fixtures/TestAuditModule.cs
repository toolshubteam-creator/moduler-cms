namespace Cms.Tests.Auditing.Fixtures;

using Cms.Abstractions.Modules;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

public sealed class TestAuditModule : ModuleBase
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "test_audit",
        Name = "Test Audit Modulu",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
        Description = "Audit + soft-delete interceptor testleri icin",
    };

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAuditableEntity>(b =>
        {
            b.ToTable("Test_AuditableEntities");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.SecretField).HasMaxLength(200);
        });

        modelBuilder.Entity<TestSoftDeletableEntity>(b =>
        {
            b.ToTable("Test_SoftDeletableEntities");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
    }
}
