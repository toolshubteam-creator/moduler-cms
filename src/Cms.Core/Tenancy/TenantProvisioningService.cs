namespace Cms.Core.Tenancy;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

public sealed class TenantProvisioningService(
    MasterDbContext db,
    ITenantDbContextFactory tenantFactory,
    IOptions<TenancyOptions> options,
    ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
{
    private readonly TenancyOptions _options = options.Value;

    public async Task<TenantProvisioningResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slugValidation = SlugValidator.Validate(request.Slug, _options.ReservedSlugs);
        if (!slugValidation.IsValid)
        {
            return TenantProvisioningResult.InvalidSlug(slugValidation.ErrorMessage!);
        }

        var slug = slugValidation.Normalized!;

        var exists = await db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == slug, cancellationToken);
        if (exists)
        {
            return TenantProvisioningResult.SlugAlreadyExists(slug);
        }

        var dbName = _options.Connection.DatabasePrefix + slug;
        var tenantConnStr = BuildTenantConnectionString(dbName);

        if (_options.AutoCreateDatabase)
        {
            try
            {
                await CreateDatabaseAsync(dbName, cancellationToken);
            }
            catch (MySqlException ex)
            {
                logger.LogError(ex, "Tenant DB '{DbName}' olusturulamadi", dbName);
                return TenantProvisioningResult.DatabaseCreationFailed(ex.Message);
            }

            try
            {
                await using var tenantDb = tenantFactory.Create(tenantConnStr);
                await tenantDb.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tenant migration patladi, '{DbName}' temizleniyor", dbName);
                await TryDropDatabaseAsync(dbName);
                return TenantProvisioningResult.MigrationFailed(ex.Message);
            }
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? slug : request.DisplayName.Trim(),
            ConnectionString = tenantConnStr,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return TenantProvisioningResult.Success(tenant);
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        if (!tenant.IsActive)
        {
            return true;
        }

        tenant.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.Tenants.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderBy(t => t.Slug).ToListAsync(cancellationToken);
    }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        return db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == normalized, cancellationToken);
    }

    private string BuildTenantConnectionString(string dbName)
    {
        var c = _options.Connection;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = c.Server,
            Port = (uint)c.Port,
            UserID = c.Uid,
            Password = c.Pwd,
            Database = dbName,
            CharacterSet = "utf8mb4",
            SslMode = MySqlSslMode.None,
            AllowPublicKeyRetrieval = true,
        };
        return builder.ConnectionString;
    }

    private async Task CreateDatabaseAsync(string dbName, CancellationToken cancellationToken)
    {
        var masterConn = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Master connection string okunamadi.");

        var builder = new MySqlConnectionStringBuilder(masterConn) { Database = string.Empty };

        await using var conn = new MySqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task TryDropDatabaseAsync(string dbName)
    {
        try
        {
            var masterConn = db.Database.GetConnectionString();
            if (string.IsNullOrEmpty(masterConn))
            {
                return;
            }

            var builder = new MySqlConnectionStringBuilder(masterConn) { Database = string.Empty };
            await using var conn = new MySqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{dbName}`;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex)
        {
            logger.LogWarning(ex, "Orphan tenant DB '{DbName}' temizlenemedi, manuel mudahale gerek", dbName);
        }
    }
}
