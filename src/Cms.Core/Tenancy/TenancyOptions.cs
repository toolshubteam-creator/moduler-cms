namespace Cms.Core.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public string RootDomain { get; set; } = "cms.local";

    public bool AllowQueryFallback { get; set; }

    public IReadOnlyList<string> BypassPaths { get; set; } = ["/Account", "/admin"];

    public TenantConnectionOptions Connection { get; set; } = new();

    public bool AutoCreateDatabase { get; set; }

    public IReadOnlyList<string> ReservedSlugs { get; set; } =
    [
        "admin", "account", "api", "static", "assets", "health", "setup", "www", "mail",
        "select", "from", "where", "database", "schema", "table", "user", "users", "root", "mysql",
        "test",
    ];
}

public sealed class TenantConnectionOptions
{
    public string Server { get; set; } = "localhost";

    public int Port { get; set; } = 3306;

    public string Uid { get; set; } = "cms_dev";

    public string Pwd { get; set; } = string.Empty;

    public string DatabasePrefix { get; set; } = "cms_tenant_";
}
