namespace Cms.Core.Tenancy;

using Cms.Core.Data.Entities;

public enum TenantProvisioningOutcome
{
    Success,
    InvalidSlug,
    SlugAlreadyExists,
    DatabaseCreationFailed,
    MigrationFailed,
}

public sealed record TenantProvisioningResult(TenantProvisioningOutcome Outcome, Tenant? Tenant, string? ErrorMessage)
{
    public bool IsSuccess => Outcome == TenantProvisioningOutcome.Success && Tenant is not null;

    public static TenantProvisioningResult Success(Tenant tenant) => new(TenantProvisioningOutcome.Success, tenant, null);

    public static TenantProvisioningResult InvalidSlug(string error) => new(TenantProvisioningOutcome.InvalidSlug, null, error);

    public static TenantProvisioningResult SlugAlreadyExists(string slug) => new(TenantProvisioningOutcome.SlugAlreadyExists, null, $"'{slug}' slug'i zaten kullaniliyor.");

    public static TenantProvisioningResult DatabaseCreationFailed(string error) => new(TenantProvisioningOutcome.DatabaseCreationFailed, null, error);

    public static TenantProvisioningResult MigrationFailed(string error) => new(TenantProvisioningOutcome.MigrationFailed, null, error);
}
