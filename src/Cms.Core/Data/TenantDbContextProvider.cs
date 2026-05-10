namespace Cms.Core.Data;

using Cms.Core.Tenancy;

internal sealed class TenantDbContextProvider(
    ITenantContext tenantContext,
    ITenantDbContextFactory factory) : IDisposable, IAsyncDisposable
{
    private TenantDbContext? _context;

    public TenantDbContext Get()
    {
        if (_context is not null)
        {
            return _context;
        }

        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException(
                "TenantDbContext kullanilmadan once tenant resolution tamamlanmis olmali. " +
                "Bu istek bypass path'te calisiyorsa TenantDbContext'i kullanma.");
        }

        var connStr = tenantContext.Current!.ConnectionString;
        _context = factory.Create(connStr);
        return _context;
    }

    public void Dispose() => _context?.Dispose();

    public ValueTask DisposeAsync() => _context?.DisposeAsync() ?? ValueTask.CompletedTask;
}
