namespace Cms.Core.Domain.Auditing;

/// <summary>
/// Bu arayuzu implement eden entity'lerin <see cref="Microsoft.EntityFrameworkCore.EntityState.Deleted"/>
/// durumu, <see cref="SoftDeleteInterceptor"/> tarafindan
/// <see cref="Microsoft.EntityFrameworkCore.EntityState.Modified"/>'a cevrilir;
/// row fiziksel olarak silinmez, IsDeleted=true ve DeletedAt=UtcNow yazilir.
/// TenantDbContext.OnModelCreating'in sonunda otomatik HasQueryFilter eklenir.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }
}
