namespace Cms.Tests.Core.Tenancy;

using System.Globalization;
using Cms.Core.Data;
using Cms.Core.Tenancy;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

[Collection(MySqlCollection.Name)]
public class TenantProvisioningServiceTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _masterConnStr = string.Empty;
    private MySqlConnectionStringBuilder _serverConn = null!;

    public async Task InitializeAsync()
    {
        _masterConnStr = await fixture.CreateDatabaseAsync("provmaster");
        _serverConn = new MySqlConnectionStringBuilder(_masterConnStr);
    }

    public async Task DisposeAsync()
    {
        await fixture.DropDatabaseAsync(_masterConnStr);

        var serverOnly = new MySqlConnectionStringBuilder(_masterConnStr) { Database = string.Empty };
        await using var conn = new MySqlConnection(serverOnly.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SHOW DATABASES LIKE 'provtest\\_%'";
        var toDrop = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                toDrop.Add(reader.GetString(0));
            }
        }

        foreach (var name in toDrop)
        {
            await using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS `{name}`;";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    private MasterDbContext CreateMasterContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseMySql(_masterConnStr, ServerVersion.AutoDetect(_masterConnStr))
            .Options;
        return new MasterDbContext(options);
    }

    private TenantProvisioningService CreateSut(MasterDbContext masterDb, bool autoCreate = true)
    {
        var options = Options.Create(new TenancyOptions
        {
            AutoCreateDatabase = autoCreate,
            Connection = new TenantConnectionOptions
            {
                Server = _serverConn.Server,
                Port = (int)_serverConn.Port,
                Uid = _serverConn.UserID,
                Pwd = _serverConn.Password,
                DatabasePrefix = "provtest_",
            },
        });
        var factory = new TenantDbContextFactory([]);
        return new TenantProvisioningService(masterDb, factory, options, NullLogger<TenantProvisioningService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_ValidSlug_CreatesTenantAndDatabase()
    {
        await using var masterCtx = CreateMasterContext();
        await masterCtx.Database.MigrateAsync();
        var sut = CreateSut(masterCtx);

        var result = await sut.CreateAsync(new CreateTenantRequest("acme", "Acme Corp"));

        result.IsSuccess.Should().BeTrue();
        result.Tenant!.Slug.Should().Be("acme");
        result.Tenant.IsActive.Should().BeTrue();

        var serverOnly = new MySqlConnectionStringBuilder(_masterConnStr) { Database = string.Empty };
        await using var conn = new MySqlConnection(serverOnly.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'provtest_acme';";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        count.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_InvalidSlug_ReturnsInvalidSlugOutcome()
    {
        await using var masterCtx = CreateMasterContext();
        await masterCtx.Database.MigrateAsync();
        var sut = CreateSut(masterCtx);

        var result = await sut.CreateAsync(new CreateTenantRequest("AC", "Bad"));

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(TenantProvisioningOutcome.InvalidSlug);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_ReturnsSlugAlreadyExists()
    {
        await using var masterCtx = CreateMasterContext();
        await masterCtx.Database.MigrateAsync();
        var sut = CreateSut(masterCtx);

        await sut.CreateAsync(new CreateTenantRequest("contoso", "First"));
        var result = await sut.CreateAsync(new CreateTenantRequest("contoso", "Second"));

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(TenantProvisioningOutcome.SlugAlreadyExists);
    }

    [Fact]
    public async Task DeactivateAsync_ExistingTenant_SetsIsActiveFalse()
    {
        await using var masterCtx = CreateMasterContext();
        await masterCtx.Database.MigrateAsync();
        var sut = CreateSut(masterCtx);

        var created = await sut.CreateAsync(new CreateTenantRequest("foo", "Foo"));
        var deactivated = await sut.DeactivateAsync(created.Tenant!.Id);

        deactivated.Should().BeTrue();
        var fromDb = await sut.GetByIdAsync(created.Tenant!.Id);
        fromDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_FiltersInactiveByDefault()
    {
        await using var masterCtx = CreateMasterContext();
        await masterCtx.Database.MigrateAsync();
        var sut = CreateSut(masterCtx);

        await sut.CreateAsync(new CreateTenantRequest("alpha", "Alpha"));
        var b = await sut.CreateAsync(new CreateTenantRequest("bravo", "Bravo"));
        await sut.DeactivateAsync(b.Tenant!.Id);

        var activeOnly = await sut.ListAsync(includeInactive: false);
        activeOnly.Should().HaveCount(1);
        activeOnly[0].Slug.Should().Be("alpha");

        var all = await sut.ListAsync(includeInactive: true);
        all.Should().HaveCount(2);
    }
}
