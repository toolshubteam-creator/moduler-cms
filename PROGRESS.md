# PROGRESS.md — Mevcut Durum

> Bu dosya **canlı** durum dosyasıdır. Her adım sonunda güncellenir.
> Sade tutuyoruz; detaylı task listesi her fazın açılışında konuşulup üretilir.

**Son güncelleme:** Faz-2.2 (TenantDbContext factory) — DONE

---

## Faz Yol Haritası

| Faz | Tema | Süre Hedefi | Durum |
|---|---|---|---|
| **Faz-0** | Repo + doküman seti kurulumu | 1 gün | 🟢 DONE |
| **Faz-1** | Çekirdek temel (IModule, ModuleLoader, Auth) | 2 hafta | 🟢 DONE |
| **Faz-2** | Multi-tenancy + RBAC | 2 hafta | 🟡 IN-PROGRESS |
| **Faz-3** | Generic CRUD + Audit | 2 hafta | ⚪ TODO |
| **Faz-4** | Media + SEO + Settings (3 çekirdek modül) | 2 hafta | ⚪ TODO |
| **Faz-5** | Blog modülü (full, referans modül) | 2 hafta | ⚪ TODO |
| **Faz-6** | Event Bus + Notification | 2 hafta | ⚪ TODO |
| **Faz-7** | Form Builder + Page Builder | 2 hafta | ⚪ TODO |
| **Faz-8** | Production hardening + ilk müşteri pilot | 2 hafta | ⚪ TODO |

**Durum işaretleri:** 🟢 DONE · 🟡 IN-PROGRESS · ⚪ TODO · 🔴 BLOCKED

---

## Aktif Faz: Faz-2 — Multi-tenancy + RBAC

**Hedef:** Tenant resolver (subdomain bazli), RBAC (rol ve permission kontrolu), TenantDbContext factory, ilk tenant yaratma akisi. Faz-1.5'te hazirlanan IUserService ve cookie auth uzerine kurulacak.

### Faz-1 Adımları (KAPANDI)

| Adım | Başlık | Durum |
|---|---|---|
| 1.1 | Solution iskeleti ve NuGet paketleri | 🟢 DONE |
| 1.2 | IModule kontrati (Cms.Abstractions) ve Manifest tipleri | 🟢 DONE |
| 1.3 | ModuleLoader (DLL discovery + reflection) | 🟢 DONE |
| 1.4 | Auth tablolari ve hashing | 🟢 DONE |
| 1.5 | Auth login/logout MVC controller | 🟢 DONE |

### Faz-2 Adımları

| Adım | Başlık | Durum |
|---|---|---|
| 2.1 | Tenant resolver + ITenantContext | 🟢 DONE |
| 2.2 | TenantDbContext factory | 🟢 DONE |
| 2.3 | RBAC: Permission system + [HasPermission] | ⚪ TODO |
| 2.4 | Tenant CRUD + ilk tenant olusturma | ⚪ TODO |

---

## Yapılanlar (kronolojik, en yeni üstte)

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

- **Faz-2.3:** RBAC: Permission system, [HasPermission] attribute, role assignment UI/API.
- **Faz-2.4:** Tenant CRUD + ilk tenant olusturma akisi. D-005 (composition root rework) burada kapanir.

---

## Sürüm ve Etiketler

> Her faz tamamlandığında git tag'i atılır: `v0.1.0` (Faz-1 sonu), `v0.2.0` (Faz-2 sonu)…
> **v0.1.0** atildi (Faz-1 sonu, manuel push'landi). Sonraki: v0.2.0 (Faz-2 sonu).

---

## Notlar

- **Çalışma ritmi:** günde ~5 saat, haftada 5 gün, hafta sonu çalışma yok
- **Toplam hedef:** 16 hafta (Faz-1'den Faz-8'e)
- **Hedef GA:** Faz-8 sonu, ilk müşteri pilot deploy
