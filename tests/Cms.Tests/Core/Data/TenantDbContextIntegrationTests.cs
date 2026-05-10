namespace Cms.Tests.Core.Data;

using System.Globalization;
using Cms.Core.Data;
using Cms.Core.Modules;
using Cms.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection(MySqlCollection.Name)]
public class TenantDbContextIntegrationTests(MySqlContainerFixture fixture) : IAsyncLifetime
{
    private string _connStr = string.Empty;

    public async Task InitializeAsync()
    {
        _connStr = await fixture.CreateDatabaseAsync("tenant");
    }

    public Task DisposeAsync() => fixture.DropDatabaseAsync(_connStr);

    [Fact]
    public async Task Migrate_EmptyTenant_CreatesEFMigrationsHistory()
    {
        var factory = new TenantDbContextFactory(new List<ModuleDescriptor>());
        await using var ctx = factory.Create(_connStr);

        await ctx.Database.MigrateAsync();

        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '__EFMigrationsHistory';";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        count.Should().Be(1);
    }

    [Fact]
    public async Task Factory_WithoutModules_CreatesContextSuccessfully()
    {
        var factory = new TenantDbContextFactory(new List<ModuleDescriptor>());

        await using var ctx = factory.Create(_connStr);

        ctx.Should().NotBeNull();
        ctx.Database.IsRelational().Should().BeTrue();
    }
}
