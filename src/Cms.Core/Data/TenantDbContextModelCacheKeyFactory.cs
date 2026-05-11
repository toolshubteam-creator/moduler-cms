namespace Cms.Core.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Modul setine duyarli model cache anahtari. Cekirdek EF Core <see cref="ModelCacheKeyFactory"/>
/// sadece context tipini key olarak kullanir; bu da ayni <see cref="TenantDbContext"/> uzerinde
/// farkli modul setlerinin (modul aktivasyonu/deaktivasyonu, multi-tenant farkli profil)
/// ilk yuklenen modelin sonsuza dek kullanilmasina yol acar.
/// </summary>
internal sealed class TenantDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is TenantDbContext tenantContext)
        {
            return new TenantModelCacheKey(typeof(TenantDbContext), designTime, tenantContext.ModuleSetCacheKey);
        }

        return new ModelCacheKey(context, designTime);
    }

    private sealed record TenantModelCacheKey(Type ContextType, bool DesignTime, string ModuleSet);
}
