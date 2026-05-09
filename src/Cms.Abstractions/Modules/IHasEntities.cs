namespace Cms.Abstractions.Modules;

using Microsoft.EntityFrameworkCore;

public interface IHasEntities
{
    /// <summary>EF Core entity konfigurasyonlarini kaydet. Tablo isimleri modul prefix'i ile baslamalidir.</summary>
    void RegisterEntities(ModelBuilder modelBuilder);
}
