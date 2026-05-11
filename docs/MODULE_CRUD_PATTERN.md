# MODULE_CRUD_PATTERN.md — Modul Yazarlari Icin CRUD Kilavuzu

> Bu dosya Faz-5'te Blog modulu yazilirken referans olarak kullanilir.
> Faz-3'te kurulan generic CRUD altyapisini modul perspektifinden anlatir.

## Genel Akis

Bir modul kendi entity'lerini yazar, IHasEntities.RegisterEntities ile EF Core'a kayit eder. Altyapi (audit, soft-delete, permission cache) otomatik calisir.

## 1. Entity Yazimi

### Standart entity

```csharp
public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
```

Audit ve soft-delete istenmiyorsa marker interface eklenmez. Sade.

### Audit'li entity

```csharp
public class BlogPost : IAuditable
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
```

IAuditable marker interface'i implement edildiginde:

- Create/Update/Delete otomatik Audit_Entries tablosuna yazilir
- Update'te field-level diff JSON column'a kaydedilir
- UserId ICurrentUserService'ten otomatik gelir (auth'lu istekte)
- Audit insert main entity ile ayni transaction'a sarilir (D-017); fail durumunda her ikisi de rollback

### Soft-delete'li entity

```csharp
public class BlogPost : IAuditable, ISoftDeletable
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

ISoftDeletable implement edildiginde:

- `ctx.Remove(post)` cagrildiginda entity SILINMEZ; IsDeleted=true, DeletedAt=UtcNow set edilir
- Audit'te Action=Delete olarak loglanir (Update degil)
- Default sorgular silinmis kayitlari getirmez (global query filter)
- Restore: `post.IsDeleted = false; ctx.SaveChanges()` → Audit Action=Restore
- Admin `/Admin/SoftDelete` UI'dan manuel restore yapilabilir

### Sensitive field'lari audit'ten cikar

```csharp
public class User : IAuditable
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    [AuditIgnore]
    public string PasswordHash { get; set; } = string.Empty;
}
```

`[AuditIgnore]` ile isaretli property audit JSON'unda gozukmez (eski/yeni deger leak'ini onler).

## 2. Modul Entity Kaydi

Modul IHasEntities implement eder:

```csharp
public class BlogModule : ModuleBase, IHasEntities
{
    public override ModuleManifest Manifest { get; } = new()
    {
        Id = "blog",
        Name = "Blog",
        Version = NuGetVersion.Parse("1.0.0"),
        MinimumCoreVersion = NuGetVersion.Parse("1.0.0"),
    };

    public override void RegisterEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlogPost>(b =>
        {
            b.ToTable("Blog_Posts");  // CLAUDE.md Kural 4: modul prefix'i
            b.HasKey(p => p.Id);
            b.Property(p => p.Title).HasMaxLength(200).IsRequired();
        });
    }
}
```

**ISoftDeletable entity'leri otomatik kesfedilir** — modul yazarinin ek bir konfigurasyon yapmasina gerek yok. TenantDbContext.OnModelCreating sonunda `ApplySoftDeleteFilters` tum ISoftDeletable entity'lere query filter ekler; `TenantDbContextFactory.Create` her cagrida ModuleDescriptorRegistry'yi populate eder, SoftDelete admin UI dropdown'unda Blog_Posts otomatik gozukur.

## 3. Migration

Modul migration'lari `Cms.Core/Data/Migrations/Tenant/` altinda yer alir (TenantDbContext modul entity'lerini ortak yonetir). Migration komutu:

```powershell
dotnet ef migrations add Blog_AddPosts `
    --project src/Cms.Core --startup-project src/Cms.Web `
    --context TenantDbContext --output-dir Data/Migrations/Tenant
```

Migration uygulamasi: yeni tenant `TenantProvisioningService.CreateAsync` ile yaratildiginda otomatik `MigrateAsync`. Mevcut tenant'lara migration apply icin app start auto-migrate (IsDevelopment) veya admin "Tum Tenant'lari Migrate Et" butonu (`/Admin/Tenants`).

**Karar (Faz-4.1):** Modul migration'lari ortak `Cms.Core/Data/Migrations/Tenant/` klasorunde durur. Adlandirma konvansiyonu: `Yyyymmddhhmm_ModuleId_Description` (orn. `20260512_Settings_AddEntries`). Tek `__EFMigrationsHistory` tablosu; tum modul migration'lari sequential timestamp ile uygulanir. Modul kaldirma temizligi (rollback migration) Faz-8 (D-006 hot-reload) ile birlikte cozulecek.

## 4. Controller Yazimi

```csharp
[Area("Blog")]
[Authorize]
[HasPermission("blog.posts.view")]
public class PostsController(TenantDbContextProvider ctxProvider) : Controller
{
    public async Task<IActionResult> Index()
    {
        var ctx = ctxProvider.Get();
        var posts = await ctx.Set<BlogPost>().ToListAsync();
        return View(posts);
    }

    [HasPermission("blog.posts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(BlogPost post)
    {
        var ctx = ctxProvider.Get();
        ctx.Add(post);
        await ctx.SaveChangesAsync();
        // Audit OTOMATIK yazildi (interceptor) — main entity + audit row ayni transaction
        return RedirectToAction(nameof(Index));
    }
}
```

**Onemli:** ITenantContext zaten resolve edilmis olmali (TenantResolutionMiddleware modul route'larinda calisir). `/Account` ve `/admin` path'leri tenant-bypass — modul controller'lari tenant subdomain'inde calisir.

**Route konvansiyonu (Faz-4.1):** Default areas route pattern `{area:exists}/{controller=Home}/{action=Index}/{id?}` en az 2 path segment ister. Tek-segment URL (orn. `/Settings`) area route'una uymaz cunku constraint 2+ segment bekler. Modul kendini `/{Area}/{Controller}` ile expose eder — Settings modulu icin `/Settings/Settings`, Blog modulu icin `/Blog/Posts` gibi. `/Admin/Audit` ve `/Admin/Tenants` (Faz-3.2) ile simetrik. Tek-segment "kisa URL" istenirse modul `MapEndpoints` (IHasEndpoints) ile explicit endpoint kaydedebilir.

**TBD (Faz-5'te netlesecek):** Modul controller'lari icin tenant resolution disinda calistirma senaryosu (orn. background job icin) — TenantDbContextProvider tenant olmadan exception atar. Hangfire job pattern Faz-6'da tartisilacak.

## 5. Permission Konvansiyonu

- Key formati: `{module_id}.{resource}.{action}` lowercase
- Ornek: `blog.posts.view`, `blog.posts.create`, `blog.comments.moderate`
- Permission tanimi: modul `GetPermissions()` metodunda doner; PermissionSeeder app start'ta seed eder
- `core` prefix REZERVE — modullerde kullanilamaz (PermissionSeeder modul id `core` ise modulu atlar)

```csharp
public class BlogModule : ModuleBase, IHasPermissions
{
    public override IReadOnlyList<PermissionDescriptor> GetPermissions() =>
    [
        new() { Key = "blog.posts.view", DisplayName = "Blog Yazilarini Goruntule" },
        new() { Key = "blog.posts.create", DisplayName = "Blog Yazisi Olustur" },
        new() { Key = "blog.posts.delete", DisplayName = "Blog Yazisi Sil" },
    ];
}
```

Permission kontrolu CachedPermissionService uzerinden yapilir (5dk sliding cache, FrozenSet<string>). Rol/permission degisikliginde `IPermissionCacheInvalidator.InvalidateUser` / `InvalidateRoleAsync` ile cache invalidate edilir; seed sonrasi `InvalidateAll` otomatik.

## 6. Dokunma Listesi

Asagidakileri YAPMA — cekirdek davranisi bozar:

- ❌ TenantDbContext'e modul entity'si icin DbSet ekleme — `IHasEntities.RegisterEntities` kullan
- ❌ AuditEntry tablosuna kayit manuel ekleme — interceptor zaten yaziyor
- ❌ ISoftDeletable entity'sinde `ctx.Database.ExecuteSqlRaw("DELETE ...")` — query filter bypass olur, audit yazilmaz
- ❌ Permission key'i `core.` ile baslatma — rezerve
- ❌ Master DB'ye dogrudan baglanma — modul her zaman tenant DB ile calisir
- ❌ TenantDbContext'i constructor ile inject etme — TenantDbContextProvider kullan (per-request lazy)
- ❌ `ctx.FindAsync(...)` ile soft-deleted entity arama — EF Core 9 davranisi belirsiz, `IgnoreQueryFilters().FirstOrDefaultAsync` kullan
- ❌ MVC parametre adlari icin `action` / `controller` / `area` — route token collision (Faz-3.2 FIX). `auditAction` + `[FromQuery(Name="act")]` gibi rename
- ❌ Modul DLL'i tek `Copy($(TargetPath))` ile kopyalama — `.deps.json` ve transitif `.dll`'ler eksik kalir, AssemblyDependencyResolver runtime'da fail eder (Faz-4.1 FIX). `src/Modules/Directory.Build.targets` glob pattern (`$(TargetDir)*.dll`, `$(TargetDir)*.deps.json`) kullanir; modul yazarinin csproj'a Copy target eklemesine gerek yok (Directory.Build.targets her `src/Modules/*` projeye otomatik uygulanir).
- ❌ Cross-module reference icin hedef modulun ana projesine ProjectReference atma — sadece `Cms.Modules.X.Contracts` projesine referans verilir (CLAUDE.md Kural 5). Contracts.dll'leri host start'inda Default AssemblyLoadContext'e eagerly yuklenir (ModuleHostExtensions); modul yazarinin ek ALC konfigurasyonuna gerek yok (Faz-4.3 FIX).

## 7. Audit Restore Davranisi

Soft-delete'li bir entity restore edildiginde:

```csharp
var post = await ctx.Set<BlogPost>()
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
post!.IsDeleted = false;
post.DeletedAt = null;
await ctx.SaveChangesAsync();
```

AuditSaveChangesInterceptor `IsDeleted: true→false` transition'i tespit eder, AuditEntry.Action = Restore olarak yazar (Update degil). Admin `/Admin/SoftDelete` UI'da bu adim otomatik yapilir; modul kendi UI'inde elle restore kodu yazabilir.

## 7.1 Cross-Module Reference (Faz-4.3)

Modul A'nin Modul B'den okumasi — Sadece B'nin Contracts projesine referans:

```xml
<!-- Cms.Modules.Seo.csproj -->
<ProjectReference Include="..\Cms.Modules.Settings.Contracts\Cms.Modules.Settings.Contracts.csproj" />
```

```csharp
// SeoMetaService.cs
public sealed class SeoMetaService(TenantDbContext db, ISettingsService settings) : ISeoMetaService
{
    public async Task<SeoMetaResolved> ResolveAsync(string targetType, string targetId, CancellationToken ct = default)
    {
        var meta = await GetAsync(targetType, targetId, ct);
        var defaultTitle = await settings.GetAsync<string>("seo.default_title", ct);
        return new SeoMetaResolved(
            Title: meta?.Title ?? defaultTitle,
            // ...
        );
    }
}
```

Onemli noktalar:
- Sadece **Contracts**'a ref. Ana projeye (`Cms.Modules.Settings`) ref atma; build-time guard (`src/Cms.Web/Directory.Build.targets`) Cms.Web tarafindaki Modules.* referansini reddediyor ama modul-modul ProjectReference'a engel yok — Kural 5 mimari kuralidir (sadece Contracts uzerinden iletisim).
- Contracts type identity: `Cms.Modules.*.Contracts.dll`'leri host start'inda Default ALC'ye eagerly yuklenir (`ModuleHostExtensions.UseCmsModules`); birden cok modul ayni interface'i paylasir. Manuel ALC konfigurasyonu gerekmez.
- Servis sirasi: hedef modul (B) `RegisterServices`'inde implementasyonu DI'ye eklemeli; modul yukleme sirasi `IsCorePlugin` + `Manifest.Dependencies` ile topological sort yapilir. Bagimlilik bildirimi yoksa alfabetik discovery sirasi gecerlidir — DI build sirasinda tum modul registration'lari toplandiktan sonra resolution yapildigi icin sirasi onemsiz.

## 8. Ozet Tablo

| Marker / Konvansiyon | Tetikledigi Davranis |
|---|---|
| `IAuditable` | Create/Update/Delete → Audit_Entries kayit |
| `ISoftDeletable` | `Remove()` → IsDeleted=true, query filter ile gizli |
| `[AuditIgnore]` | Property audit JSON'undan dislanir |
| Tablo prefix `Module_*` | CLAUDE.md Kural 4 |
| Permission key `module_id.x.y` | Cache + seeder validation |
