# PROGRESS.md — Mevcut Durum

> Bu dosya **canlı** durum dosyasıdır. Her adım sonunda güncellenir.
> Sade tutuyoruz; detaylı task listesi her fazın açılışında konuşulup üretilir.

**Son güncelleme:** Faz-4.1 (Modul infra + Settings modulu) — DONE

---

## Faz Yol Haritası

| Faz | Tema | Süre Hedefi | Durum |
|---|---|---|---|
| **Faz-0** | Repo + doküman seti kurulumu | 1 gün | 🟢 DONE |
| **Faz-1** | Çekirdek temel (IModule, ModuleLoader, Auth) | 2 hafta | 🟢 DONE |
| **Faz-2** | Multi-tenancy + RBAC | 2 hafta | 🟢 DONE |
| **Faz-3** | Generic CRUD + Audit | 2 hafta | 🟢 DONE |
| **Faz-4** | Media + SEO + Settings (3 çekirdek modül) | 2 hafta | 🟡 IN-PROGRESS |
| **Faz-5** | Blog modülü (full, referans modül) | 2 hafta | ⚪ TODO |
| **Faz-6** | Event Bus + Notification | 2 hafta | ⚪ TODO |
| **Faz-7** | Form Builder + Page Builder | 2 hafta | ⚪ TODO |
| **Faz-8** | Production hardening + ilk müşteri pilot | 2 hafta | ⚪ TODO |

**Durum işaretleri:** 🟢 DONE · 🟡 IN-PROGRESS · ⚪ TODO · 🔴 BLOCKED

---

## Aktif Faz: Faz-4 — Media + SEO + Settings çekirdek modülleri

**Hedef:** Modul yazimi infrastructure'unu (DLL copy, RegisterServices integration) konsolide et + uc cekirdek modul (Settings, Media, SEO) ile dort yapi tasi tamamla.

### Faz-4 Adımları

| Adım | Başlık | Durum |
|---|---|---|
| 4.1 | Modul infra + Settings modulu | 🟢 DONE |
| 4.2 | Media modulu | ⚪ TODO |
| 4.3 | SEO modulu | ⚪ TODO |
| 4.4 | Retrospektif + dokumantasyon | ⚪ TODO |

---

## Faz-3 — Generic CRUD + Audit ✓ KAPANDI

**Hedef:** Modul yazimi icin generic CRUD scaffold + audit log (kim, ne zaman, neyi degistirdi). Faz-2'de hazirlanan tenant + RBAC altyapisi uzerine kurulacak. Faz-3 detayi 3.1 baslangicinda konusulacak.

### Faz-1 Adımları (KAPANDI)

| Adım | Başlık | Durum |
|---|---|---|
| 1.1 | Solution iskeleti ve NuGet paketleri | 🟢 DONE |
| 1.2 | IModule kontrati (Cms.Abstractions) ve Manifest tipleri | 🟢 DONE |
| 1.3 | ModuleLoader (DLL discovery + reflection) | 🟢 DONE |
| 1.4 | Auth tablolari ve hashing | 🟢 DONE |
| 1.5 | Auth login/logout MVC controller | 🟢 DONE |

### Faz-2 Adımları (KAPANDI)

| Adım | Başlık | Durum |
|---|---|---|
| 2.1 | Tenant resolver + ITenantContext | 🟢 DONE |
| 2.2 | TenantDbContext factory | 🟢 DONE |
| 2.3 | RBAC: Permission system + [HasPermission] | 🟢 DONE |
| 2.4 | Tenant CRUD + admin UI | 🟢 DONE |

### Faz-3 Adımları

| Adım | Başlık | Durum |
|---|---|---|
| 3.1 | Base entity contracts + EF interceptor altyapisi | 🟢 DONE |
| 3.2 | Audit log okuma + admin UI (read-only) | 🟢 DONE |
| 3.3 | Soft delete restore akisi + admin UI | 🟢 DONE |
| 3.4 | Permission cache + invalidation (D-012) | 🟢 DONE |
| 3.5 | CRUD pattern referans dokumani + retrospektif | 🟢 DONE |

---

## Yapılanlar (kronolojik, en yeni üstte)

- **Faz-4.1** (commit: `e033c08`): D-014 KAPATILDI — `UseCmsModules(WebApplicationBuilder)` extension build-oncesi modul DLL'lerini Modules/ klasorunden discover edip her IModule instance'i icin `RegisterServices(services, configuration)` cagirir, modul assembly'lerini IMvcBuilder.AddApplicationPart ile MVC controller/view discovery'ye ekler, ModuleDescriptorRegistry'yi pre-populate eder. `src/Modules/Directory.Build.targets` glob pattern ile (`$(TargetDir)*.dll` + `$(TargetDir)*.deps.json`) modul ciktilarini Cms.Web/bin/<Config>/<Tfm>/Modules/ altina kopyalar — AssemblyDependencyResolver Contracts ve transitive deps'i bulur. **Cms.Modules.Settings.Contracts** (saf interface): ISettingsService (GetAsync<T>/SetAsync<T>/GetRawAsync/GetAllAsync/DeleteAsync), SettingEntry record, SettingValueType enum (String/Int/Bool/Decimal/Json). **Cms.Modules.Settings** (Microsoft.NET.Sdk.Razor, view'lar DLL'e gomulu): SettingsModule (IsCorePlugin, settings module id, IHasEntities + IHasPermissions), SettingEntryEntity (IAuditable, Settings_Entries tablosu Key UNIQUE Value text), SettingsService (tip-spesifik Serialize/Convert + InvalidCastException), Areas/Settings/Controllers/SettingsController ([Authorize][HasPermission("settings.view")] Index + Edit + Delete), Razor Index.cshtml. ModuleLoadContext refactor: Default.LoadFromAssemblyName ile shared assembly probing — NuGet.Versioning identity ayrismasi (MissingMethodException set_Version) cozuldu. TenantDbContextDesignFactory AppContext.BaseDirectory/Modules tarayisi — `dotnet ef` migration scaffold'una modul entity'leri dahil. 10 yeni test (SettingsServiceTests integration): Create/Update/Delete + audit row assertion + tip donusum + JSON deserialize + InvalidCastException; toplam 134 test yesil. **FIX-01:** Tek `Copy($(TargetPath))` Contracts'i atliyordu — glob pattern'a gecildi. **FIX-02:** ModuleLoadContext Default.Assemblies kontrolu lazy paketleri kaciriyordu (NuGet.Versioning identity bug) — proaktif Default.LoadFromAssemblyName ile cozuldu. **FIX-03:** appsettings.json `Modules:Path = "Modules"` relative path source dir'i isaret ediyordu — silindi, default `AppContext.BaseDirectory/Modules` devreye girdi. **FIX-04:** Areas route 2+ segment ister; modul URL'leri `/Area/Controller` konvansiyonu (orn. `/Settings/Settings`).
- **Faz-3.5** (tag: `v0.3.0`): Faz-3 kapanis — `docs/MODULE_CRUD_PATTERN.md` (modul yazari perspektifinden Faz-3 altyapisi kullanim kilavuzu: entity marker'lari, migration, controller, permission konvansiyonu, dokunma listesi, restore davranisi) + `docs/FAZ-3-RETROSPECTIVE.md` (hedef vs gerceklesen, 5 alt-adim ozeti, tasarim kararlari geri bakis, beklenmedik bulgular [model cache, route collision, EF auto-create, FindAsync, sealed PermissionService, async post-eviction], ertelemeler [D-017/D-018/D-012 kapandi, D-019 dogdu, D-002 Faz-6'ya kaydi], Faz-4 devir notlari, olculer). README.md durum: Faz-0 -> Faz-3 tamamlandi. v0.3.0 annotated tag.
- **Faz-3.4** (commit: `e5af2e2`): D-012 KAPATILDI — IPermissionCacheInvalidator + MemoryPermissionCacheInvalidator (singleton, shadow index ConcurrentDictionary<string, byte> wildcard remove icin, KeyPrefix "cms:perm:", IServiceScopeFactory ile InvalidateRoleAsync scoped MasterDbContext.Sys_UserRoles join). CachedPermissionService (IPermissionService decorator, FrozenSet<string> 5dk sliding TTL, RegisterPostEvictionCallback shadow index temizleme, Debug log HIT/MISS). AddCmsAuthorization DI chain: AddMemoryCache + MemoryPermissionCacheInvalidator singleton + IPermissionCacheInvalidator facade + PermissionService concrete scoped + IPermissionService factory delegate (recursive resolve bypass). PermissionSeeder constructor 4. parametre IPermissionCacheInvalidator; ReconcileAsync sonu InvalidateAll. 8 yeni test (5 CachedPermissionService + 2 MemoryPermissionCacheInvalidator + 1 PermissionSeederTests). Toplam 124 test yesil. **FIX-01:** PermissionService sealed -> test'te inherit edilemedi (CountingPermissionService basarisiz); CachedPermissionService ctor PermissionService -> IPermissionService gevsetildi, DI factory delegate ile recursive resolve bypass. **FIX-02:** MemoryCache post-eviction callback async (Task.Factory.StartNew) — test sync flaky; polling loop (Task.Delay 20ms, 2s deadline) ile cozuldu, prod'da gorunmez. **FIX-03:** Serilog ReadFrom.Configuration "Logging" section'i okumuyor — manuel log dogrulama icin Serilog:MinimumLevel:Override ile namespace Debug'a ayarlandi, dogrulama sonrasi geri cekildi. **NOT:** Mevcut admin route'lar (Tenants/Audit/SoftDelete) SystemRole policy kullaniyor, [HasPermission] attribute hicbir route'a bagli degil — IPermissionService.HasPermissionAsync runtime'da cagrilmiyor, CachedPermissionService production'da dormant. Faz-5'te Blog modulu [HasPermission] kullanmaya basladiginda canli HIT/MISS log'lari gorulecek; infrastructure katmani 5 test (call count assertion) + startup InvalidateAll log'u ile yapisal olarak dogrulandi.
- **Faz-3.3** (commit: `4890e18`): D-018 KAPATILDI — TenantMigrationRunner (Cms.Core/Data, sequential MigrateAsync tum aktif tenant'lar, try/catch tek fail digerlerini durdurmaz, TenantMigrationReport(Successful/Failed/Total)) + Program.cs app start auto-migrate (IsDevelopment only, LoadCmsModulesAsync + PermissionSeeder sonrasi) + TenantsController.MigrateAll POST action (antiforgery, TempData success/error) + Tenants/Index.cshtml "Tum Tenant'lari Migrate Et" butonu (confirm dialog). AuditAction.Restore detection Faz-3.1'de zaten dogru yazilmis (ISoftDeletable IsDeleted true->false transition Classify metodunda yakalaniyor) — kod degisikligi gerekmedi, AuditRestoreDetectionTests ile dogrulandi (Create -> Delete -> Restore action sirali yazildi). ModuleDescriptorRegistry.GetSoftDeletableEntityTypes (ConcurrentDictionary<Type, byte> backing, snapshot getter, internal RegisterSoftDeletableTypes); populate TenantDbContextFactory.Create cross-cut'a tasindi (her Create'te ctx.Model uzerinden ISoftDeletable tipleri idempotent registry'e eklenir — OnModelCreating EF Core model cache nedeniyle bir kez calisip atlamasini bypass eder). Areas/Admin/SoftDeleteController (Authorize SystemRole; Index tenantId+entityName+page; Restore POST antiforgery; generic helpers QueryDeletedTypedAsync<T>+RestoreTypedAsync<T> with Expression.Lambda PK predicate + IgnoreQueryFilters; PK convert Guid/int/long+ChangeType fallback). SoftDeleteIndexViewModel + DeletedEntityRow record + Index.cshtml (tenant+entity dropdowns, deleted row table, per-row restore form, pagination, empty-state). CorePermissions.SoftDeleteManage (core.softdelete.manage) eklendi, PermissionSeeder otomatik seed. _AdminLayout 3 link (Tenant'lar | Audit Log | Silinmis Kayitlar). 8 yeni test (2 migration + 1 restore detection + 3 controller + 2 registry); toplam 116 test yesil. **FIX-01:** EF Core MigrateAsync non-existent DB'yi otomatik yaratti (IRelationalDatabaseCreator.CreateAsync) — fail test icin bad credentials kullanildi. **FIX-02:** SqlQueryRaw<int> SELECT COUNT(*) subquery wrap'i bozdu (AS s LIMIT 1 syntax err); direct DbConnection.ExecuteScalarAsync. **FIX-03:** ConcurrentDictionary.Keys ICollection<T> donduruyor, IReadOnlyCollection<T> gerekiyordu — [.. _softDeletableTypes.Keys] snapshot. **FIX-04:** OnModelCreating populate test class'lari arasi flaky (model cache hit -> registry bos) — populate TenantDbContextFactory.Create cross-cut'a tasindi. **FIX-05:** ctx.FindAsync soft-deleted entity'yi bulamadi (EF 9 query filter respect belirsiz) — Expression PK predicate + IgnoreQueryFilters generic helper. **NOT:** Manuel UI'da acme tenant'in Faz-2.4 oncesi bozuk conn string'i fail oldu (TenantMigrationReport 2/3 ok, 1 failed); D-018 tasariminin tam istedigi davranis (digerlerini durdurmadi). Fail nedeni admin UI'da gorunmuyor (sadece log'da) — D-019 olarak Faz-7'ye kaydedildi.
- **Faz-3.2** (commit: `477aaf2`): D-017 KAPATILDI — AuditSaveChangesInterceptor transaction-aware refactor: ConditionalWeakTable<DbContext, AuditContextState> (PendingAudits + OwnedTransaction tek nesnede); SavingChanges{Async} sonrasi BeginOwnedTransactionIfNeeded (CurrentTransaction yoksa + pending varsa BeginTransaction); SavedChanges{Async} sonrasi FlushPending + CommitOwnedTransaction; SaveChangesFailed{Async} override AbortOwnedTransaction (rollback + state cleanup). 3 transaction testi: forced fail rollback (DROP TABLE Audit_Entries + DbUpdateException, main entity yok); outer transaction nested no-op (caller BeginTransaction + Rollback, hem main hem audit geri alindi); happy path commit. Areas/Admin/AuditController (Index, Authorize SystemRole, [FromQuery(Name="act")] auditAction route token collision fix); AuditIndexViewModel + AuditFilterViewModel + TenantOption record; Index.cshtml tenant dropdown + filtre fieldset + audit tablosu + <details>/<summary> JSON expand + prev/next pagination. CorePermissions sabit (ModuleId="core" rezerve, AuditView "core.audit.view"). PermissionSeeder.ReconcileAsync core permission'larini once seed eder, "core" prefix bypass valid; modul id "core" rezerve (warning + skip). _AdminLayout'a Audit Log nav link. 8 yeni test (3 transaction + 5 controller); toplam 108 test yesil. **FIX-01:** Route token collision — `action` parametre adi ASP.NET Core default route token'i ile cakisti (route value "Index" query "Update" onunde, AuditAction binmedi). [FromQuery(Name="act")] + view name="act" + asp-route-act ile fix. Manuel cURL testinde yakalandi. **NOT:** Faz-3.1'de yaratilan Audit_Entries migration mevcut tenant'lara (acme, browsertest) uygulanmadi — yeni provision edilen audittest tenant'inda dogrulandi. Mevcut tenant migration apply gap'i D-018 olarak Faz-3.3'e kaydedildi.
- **Faz-3.1** (commit: `72f3322`): IAuditable + ISoftDeletable contract'lari (Cms.Core/Domain/Auditing) + AuditAction enum + AuditIgnoreAttribute + AuditEntry entity (tenant DB, Audit_ prefix, MySQL native JSON column for Changes) + AuditSaveChangesInterceptor (2-fazli: SavingChanges snapshot, SavedChanges PK populate + ayri SaveChanges; ConditionalWeakTable<DbContext, List<PendingAudit>> per-context state; ConcurrentDictionary reflection cache) + SoftDeleteInterceptor (Deleted->Modified+IsDeleted=true+DeletedAt=UtcNow) + ICurrentUserService (Cms.Core contract) + HttpCurrentUserService (Cms.Web impl, ClaimTypes.NameIdentifier int.TryParse) + SoftDeleteModelBuilderExtensions.ApplySoftDeleteFilters (reflection ile HasQueryFilter otomatik bagli) + AddCmsCurrentUser DI extension. TenantDbContextFactory ikinci ctor IEnumerable<IInterceptor>; ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>() — modul setine duyarli model cache (test isolation + Faz-5 dinamik modul hazirligi). AddAuditEntries migration. 21 yeni test (HttpCurrentUserService 6 + SoftDeleteInterceptor 3 + AuditInterceptor 6 + diger). Toplam 100 test yesil. **FIX-01:** EF Core model cache TenantDbContext tipini modul setinden bagimsiz paylasiyordu; TenantDbContextModelCacheKeyFactory eklendi (ModuleSetCacheKey = sirali modul id join'i anahtara dahil). **FIX-02:** Plan'daki "entry.Entity is AuditEntry" self-reference filter CS8121 (AuditEntry IAuditable degil); statik tip garantisi nedeniyle filter kaldirildi. **NOT:** Audit row main entity save'inden ayri SaveChanges ile yaziliyor — partial-failure penceresi acik; D-017 olarak Faz-3.2 basinda transaction wrapping ile kapanacak.
- **Faz-2.4** (commits: `ff84bb8` 2.4a refactor, `1d236a7` 2.4b feat, `4431871` 2.4c feat): Composition root rework — `AddCmsModuleSystem` (DI registrations) + `LoadCmsModulesAsync(IHost)` (post-build) + `ModuleDescriptorRegistry` mutable holder; `BuildServiceProvider()` anti-pattern kaldirildi (D-005 kapatildi). `TenantProvisioningService` (DB lifecycle: CREATE DATABASE + migration + orphan DROP cleanup; SlugValidator regex + reserved listesi + case-insensitive normalize). Areas/Admin/TenantsController (Index/Create/Deactivate) + 3 Razor view (_AdminLayout + Tenants/Index + Create) + CreateTenantViewModel + SystemRole policy/handler/requirement. Toplam 30 yeni test (24 SlugValidator+Provisioning + 6 TenantsController). **FIX-01 (2.4a):** test parallelization deferred D-015 olarak kayit. **FIX-02 (2.4b):** Plan'in ic tutarsizligi (ACME slug invalid bekleniyordu ama validator ToLowerInvariant ile normalize ediyor) — InlineData duzeltildi. **FIX-03 (2.4c manuel UI):** SystemRole policy fail oldu cunku Faz-1.5 dev seed Sys_UserRoles atamasini composite-PK bug'inda kaybetmisti; admin user'a Admin role'u manuel SQL ile atandi. Toplam 85 test yesil. **Manuel UI dogrulamasi browser ile tamamlandi**: login -> create tenant `browsertest` -> MySQL'de `cms_tenant_browsertest` DB olustu + InitialTenant migration uygulandi -> deactivate -> includeInactive filter calisti -> logout. **Faz-2 KAPANDI.**
- **Faz-2.3** (commit: `959d99a`): IPermissionService + PermissionService (SuperAdmin bypass: IsSystem=true rolu kontrolu atlar; tenant-scoped query: TenantId match veya null = global rol) + HasPermissionAttribute (AuthorizeAttribute miras, "perm:&lt;key&gt;" policy adi) + HasPermissionRequirement + HasPermissionHandler (ITenantContext'ten tenant id, ClaimTypes.NameIdentifier'dan userId) + HasPermissionPolicyProvider (DefaultAuthorizationPolicyProvider fallback ile dinamik on-demand policy uretimi) + PermissionSeeder (idempotent reconcile: yoksa ekler, varsa DisplayName/Description gunceller, ORPHAN PERMISSION'LARI SILMEZ; modul prefix validation) + AddCmsAuthorization DI extension + Cms.Web startup'ta seeder.ReconcileAsync(). 13 yeni test (6 PermissionService + 4 PermissionSeeder + 3 HasPermissionHandler unit). Toplam 56 test yesil.
- **Faz-2.2** (commits: `15b95c0` 2.2a refactor, `27b38e6` 2.2b feat): MySqlContainerFixture (tek container, izole DB per test, paylasilan ICollectionFixture, **5.4x test hizlanma** — 11dk -> 2dk) + TenantDbContext (modul entity'leri runtime kayit, IHasEntities.RegisterEntities cagrilir, cekirdek DbSet yok) + TenantDbContextFactory (singleton, dinamik conn string) + TenantDbContextProvider (scoped, ITenantContext'ten conn string al, lazy create, tenant resolution sart) + AddCmsTenantData DI extension + Master/Tenant migration klasor ayrimi (Data/Migrations/Master, Data/Migrations/Tenant) + InitialTenant migration (bos schema + EFMigrationsHistory) + IDesignTimeDbContextFactory&lt;TenantDbContext&gt; (dotnet ef komutlari icin) + 4 yeni test (2 factory unit + 2 TenantDbContext integration). **FIX-01 (2.2a):** EF Migrations advisory lock formati `__<dbname>_EFMigrationsLock` 64 char sinirini geciyordu (test_<prefix>_<guid32> 46 char + lock overhead 21 char = 67 > 64). DB adi `t_<prefix>_<guid16>` formatina indirilip 40 char ile sinirlandi. Toplam 43 test yesil.
- **Faz-2.1** (commit: `e299bc6`): TenancyOptions + ITenantContext (scoped, Set() bir kez) + TenantContext (internal) + ITenantResolver + SubdomainTenantResolver (host'tan slug cikarma + dev query fallback) + TenantResolutionMiddleware (bypass paths /Account, /admin; bilinmeyen tenant 404) + AddCmsTenancy DI + UseTenantResolution() pipeline + appsettings Tenancy:RootDomain/AllowQueryFallback. Cms.Core'a InternalsVisibleTo Cms.Tests eklendi (TenantContext internal). 7 resolver Testcontainers MySQL test + 3 TenantContext unit test. Toplam 39 test yesil. Manuel cURL ile dogrulandi: /Account/Login bypass=200, /=404 (root host'a tenant resolution uygulaniyor, beklenen davranis), /?tenant=acme=302 (tenant cozuldu, Authorize), /?tenant=unknown=404 + "Tenant bulunamadi.".
- **Faz-1.5** (commit: `de44a9b`): IUserService + UserService + AuthenticationResult + AccountController (Login GET/POST + Logout) + LoginViewModel + minimal Razor view'lar (_ViewImports/_ViewStart/_Layout/Account/Login) + cookie authentication (`AddCookie`, /Account/Login path) + dev admin seed (Auth:DefaultAdmin) + Auth:DefaultAdmin appsettings.Development.json + 4 UserService Testcontainers integration testi. **FIX-01:** Login E2E (WebApplicationFactory + Testcontainers MySQL) Windows reverse-DNS quirk ile patladi (4 fix denendi, tutmadi); test silindi, manuel UI ile dogrulandi (form, hatali parola error, basarili login + cookie, logout cookie iptal). DEFERRED.md D-010 olarak Faz-7'ye yazildi. **FIX-02:** Faz-1.4'teki UserRole composite PK (UserId, RoleId, TenantId?) EF Core nullable composite key kuralinda Add'i reddetti — surrogate int Id PK + (UserId, RoleId, TenantId) UNIQUE INDEX ile fix; yeni migration `FixUserRolePrimaryKey`, FK drop sirasi manuel duzeltildi. **FIX-03:** D-001 kapandi — `src/Cms.Web/Directory.Build.targets` ile Cms.Web -> Cms.Modules.* ProjectReference build-time guard, negatif test ile dogrulandi. Toplam 29 test yesil.
- **Faz-1.4** (commit: `b7f8c2d`): MasterDbContext + 6 entity (User/Role/UserRole/Permission/RolePermission/Tenant, Sys_ prefix) + Pbkdf2PasswordHasher (PBKDF2-SHA256, 100k iter, "iter.salt_b64.hash_b64" format) + AddCmsMaster DI extension + InitialAuth migration + 6 hasher unit testi + 3 MasterDbContext integration testi (Testcontainers MySQL 8.0). appsettings.Development.json git takibinden cikarildi, .gitignore'a eklendi. .editorconfig'e EF auto-generated migration dosyalari icin IDE0161 override'i eklendi. Toplam 25 test yesil.
- **Faz-1.3** (commit: `3bd6444`): ModuleLoader + ModuleLoadContext (collectible AssemblyLoadContext) + ModuleDependencyResolver (topological sort, cycle/version/missing-dep validation) + ModuleHostExtensions (AddCmsModules/MapCmsModules/InstallCmsModulesAsync) + Cms.Tests.FakeModule integration helper + 9 yeni test (7 resolver unit + 2 loader integration). AnalysisMode Recommended->Default geri alindi (CA1848/CA1873 performans kurallari suggestion seviyesinde).
- **Faz-1.2** (commit: `c976f3b`): IModule + opsiyonel arayuzler (IHasEntities/Endpoints/Permissions/MenuItems) + ModuleBase + ModuleManifest/Dependency + Permission/Menu deger object'leri + 7 value object testi. Cms.Abstractions FrameworkReference Microsoft.AspNetCore.App ile baglandi.
- **Faz-1.1** (commit: `6896aef`): Solution iskeleti + 4 proje (Cms.Abstractions, Cms.Core, Cms.Web, Cms.Tests) + Central Package Management + Mediator/Pomelo/Serilog paketleri. Release build sifir warning, sifir error.
- **Faz-0.1-0.5** (commit: `e87ee2d`): Repo kurulum + dort dokuman yerlestirme + ilk push.

---

## Sıradaki

- **Faz-4.2:** Media modulu (full) — upload, listele, sil; tenant-scoped storage path.

---

## Sürüm ve Etiketler

> Her faz tamamlandığında git tag'i atılır: `v0.1.0` (Faz-1 sonu), `v0.2.0` (Faz-2 sonu)…
> **v0.1.0** atildi (Faz-1 sonu). **v0.2.0** atildi (Faz-2 sonu). **v0.3.0** atildi (Faz-3 sonu). Faz-4 baslangici (4.1 done) — Sonraki: v0.4.0 (Faz-4 sonu, 3 cekirdek modul tamamlandiginda).

---

## Notlar

- **Çalışma ritmi:** günde ~5 saat, haftada 5 gün, hafta sonu çalışma yok
- **Toplam hedef:** 16 hafta (Faz-1'den Faz-8'e)
- **Hedef GA:** Faz-8 sonu, ilk müşteri pilot deploy
