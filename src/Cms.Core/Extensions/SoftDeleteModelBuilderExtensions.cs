namespace Cms.Core.Extensions;

using System.Linq.Expressions;
using Cms.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

public static class SoftDeleteModelBuilderExtensions
{
    /// <summary>
    /// ISoftDeletable implement eden tum entity tiplerine
    /// <c>e =&gt; !((ISoftDeletable)e).IsDeleted</c> HasQueryFilter ekler.
    /// TenantDbContext.OnModelCreating'in SONUNDA (modul entity'leri kayit edildikten sonra)
    /// cagrilmalidir; aksi halde modul entity'leri filtre olmadan kalir.
    /// </summary>
    public static void ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                continue;
            }

            var param = Expression.Parameter(clrType, "e");
            var cast = Expression.Convert(param, typeof(ISoftDeletable));
            var isDeleted = Expression.Property(cast, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);
            var lambda = Expression.Lambda(notDeleted, param);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }
}
