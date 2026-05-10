namespace Cms.Core.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public string RootDomain { get; set; } = "cms.local";

    public bool AllowQueryFallback { get; set; }

    public IReadOnlyList<string> BypassPaths { get; set; } = ["/Account", "/admin"];
}
