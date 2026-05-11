namespace Cms.Tests.Core.Data;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Modules;
using Cms.Core.Tenancy;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

[Collection(MySqlCollection.Name)]
public class TenantMigrationRunnerTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private readonly List<string> _provisionedDbs = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var conn in _provisionedDbs)
        {
            await fixture.DropDatabaseAsync(conn);
        }
    }

    private async Task<string> CreateBlankTenantDbAsync()
    {
        var conn = await fixture.CreateDatabaseAsync("migrun");
        _provisionedDbs.Add(conn);
        return conn;
    }

    private static Tenant BuildTenant(string slug, string connStr) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        DisplayName = slug,
        ConnectionString = connStr,
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private static TenantDbContextFactory BuildFactory() => new(new List<ModuleDescriptor>());

    [Fact]
    public async Task MigrateAllTenants_AppliesPendingMigrationsToStaleTenants()
    {
        var dbA = await CreateBlankTenantDbAsync();
        var dbB = await CreateBlankTenantDbAsync();
        var tenants = new[] { BuildTenant("a", dbA), BuildTenant("b", dbB) };

        var factory = BuildFactory();
        var runner = new TenantMigrationRunner(
            NullLogger<TenantMigrationRunner>.Instance,
            new StubProvisioningService(tenants),
            factory);

        var report = await runner.MigrateAllTenantsAsync();

        report.Successful.Should().Be(2);
        report.Failed.Should().Be(0);
        report.Total.Should().Be(2);

        // Audit_Entries her iki DB'de de mevcut olmali — direct ADO.NET kontrolu
        foreach (var conn in new[] { dbA, dbB })
        {
            await using var verify = factory.Create(conn);
            var dbConn = verify.Database.GetDbConnection();
            await dbConn.OpenAsync();
            await using var cmd = dbConn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'Audit_Entries'";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            count.Should().Be(1, $"Audit_Entries tablosu {conn} icinde olmali");
        }
    }

    [Fact]
    public async Task MigrateAllTenants_ContinuesOnSingleTenantFailure()
    {
        var dbA = await CreateBlankTenantDbAsync();
        var dbC = await CreateBlankTenantDbAsync();

        // Ortadaki tenant'in conn string'i gecersiz kullanici — kimlik dogrulama hatasi
        // (EF Core MigrateAsync eksik DB'yi otomatik yaratir, bu yuzden Database adi degisikligi
        // sahte fail uretmez; auth basarisizligi gercek migration patlamasi simule eder).
        var badConn = new MySqlConnectionStringBuilder(dbA)
        {
            UserID = "no_such_user_for_test",
            Password = "definitely_wrong",
        }.ConnectionString;

        var tenants = new[]
        {
            BuildTenant("alpha", dbA),
            BuildTenant("broken", badConn),
            BuildTenant("gamma", dbC),
        };

        var factory = BuildFactory();
        var runner = new TenantMigrationRunner(
            NullLogger<TenantMigrationRunner>.Instance,
            new StubProvisioningService(tenants),
            factory);

        var report = await runner.MigrateAllTenantsAsync();

        report.Total.Should().Be(3);
        report.Successful.Should().Be(2);
        report.Failed.Should().Be(1);
    }

    private sealed class StubProvisioningService(IReadOnlyList<Tenant> tenants) : ITenantProvisioningService
    {
        public Task<TenantProvisioningResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
            => Task.FromResult(tenants);

        public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<Tenant?>(tenants.FirstOrDefault(t => t.Id == tenantId));

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
