namespace Cms.Tests.Infrastructure;

using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

public sealed class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("placeholder")
        .WithUsername("root")
        .WithPassword("Test_Root_Password_2026!")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync(string? namePrefix = null)
    {
        // Pomelo MySQL EF advisory lock adi formati: "__<dbname>_EFMigrationsLock" (max 64 char).
        // dbname icin pratik sinir: 64 - 2 - len("_EFMigrationsLock") = 64 - 21 = 43.
        var shortId = Guid.NewGuid().ToString("N")[..16];
        var dbName = $"t_{namePrefix ?? "x"}_{shortId}".ToLowerInvariant();
        if (dbName.Length > 40)
        {
            dbName = dbName[..40];
        }

        var rootConnStr = _container.GetConnectionString();
        await using (var conn = new MySqlConnection(rootConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new MySqlConnectionStringBuilder(rootConnStr) { Database = dbName };
        return builder.ConnectionString;
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var dbName = builder.Database;
        builder.Database = string.Empty;

        await using var conn = new MySqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{dbName}`;";
        await cmd.ExecuteNonQueryAsync();
    }
}
