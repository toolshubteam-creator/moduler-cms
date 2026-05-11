# FAZ-3-RETROSPECTIVE.md — Generic CRUD + Audit + Soft-Delete

**Faz suresi:** Faz-3.1'den 3.5'e
**Faz tag:** v0.3.0
**Onceki tag:** v0.2.0 (Faz-2 sonu)

## Hedef vs Gerceklesen

**Hedef:** Modul yazarlari icin generic CRUD altyapisi — audit log, soft-delete, permission cache.

**Gerceklesen:** Hedef tam karsilandi. Bes alt-adim (3.1-3.5), bes kod commit + bes doc commit. 24 yeni test (100 → 124). Faz-3 acilisinda 13 erteleme vardi, faz sonunda 13 (D-017 + D-018 + D-012 kapandi, D-019 dogdu). D-002 Hangfire Faz-6'ya kaydi (audit ic-transaction cozumu D-017'de yapildi, Hangfire'a gerek kalmadi).

## Alt-Adim Ozeti

| Adim | Tema | Sonuc |
|---|---|---|
| 3.1 | Base entity contracts + interceptor altyapisi | IAuditable/ISoftDeletable/AuditIgnore/AuditEntry/2 interceptor/ICurrentUserService — 21 test |
| 3.2 | D-017 transaction wrapping + Audit log read UI | Interceptor transaction-aware, AuditController + tenant dropdown — 8 test |
| 3.3 | D-018 tenant migration apply + Soft-delete restore UI | TenantMigrationRunner (sequential, fail-tolerant) + SoftDeleteController + generic restore helper — 8 test |
| 3.4 | D-012 permission cache + invalidation | CachedPermissionService decorator + MemoryPermissionCacheInvalidator (shadow index) — 8 test |
| 3.5 | Dokuman + retrospektif | Bu dosya + MODULE_CRUD_PATTERN.md |

## Tasarim Kararlari — Geri Bakis

### 1. Generic CRUD seviyesi (Faz-3 acilisinda: C secildi)

**Secim:** Minimal interceptor-based, generic CrudController YOK.

**Geri bakis:** Dogru karar. Modul yazarinin kendi controller'ini yazmasi zorlamadi; sadece marker interface implement etmesi yeterli. Faz-5'te Blog modulu yazilirken pattern'in eksiklikleri (orn. generic restore UI per-modul mu?) gorulecek.

### 2. Audit transaction yaklasimi (Faz-3.1: ayri SaveChanges, Faz-3.2: aynileridi)

**Hata:** Faz-3.1'de "audit ic-transaction" demistik ama implementasyon 2 ayri SaveChanges oldu (PK populate nedeniyle). Faz-3.2'de D-017 ile gercek transaction wrapping eklendi.

**Geri bakis:** Faz-3.1 acilisinda "Karar 2'de aynideris dedik, implementasyon farkli kalti" hatasi yapildi. Manuel review eksigi. Faz-3.2'de farkedildi, D-017 ile temizlendi — ama bu ilk denemede gozden kacirilmasi gereken bir kalibre noktasi.

### 3. Tenant migration apply stratejisi (Faz-3.3: hibrit)

**Secim:** Dev'de app start auto + admin "Migrate All" butonu. Production'da sadece manuel.

**Geri bakis:** Hibrit isabetli. acme tenant'in bozuk conn string'i runner'i durdurmadi (D-018 senaryosu tam istedigi gibi calisti). Fail nedeni admin UI'da gorunmuyor — D-019 olarak Faz-7'ye kayitli.

### 4. Permission cache decorator (Faz-3.4: decorator)

**Secim:** CachedPermissionService decorator, original PermissionService kalir.

**Geri bakis:** Dogru. PermissionService sealed oldugu icin inherit test fail oldu, ama decorator IPermissionService uzerinden calisti, sealed kalmasi korundu. Faz-5'te dormant CachedPermissionService canlanacak.

## Beklenmedik Bulgular

1. **EF Core model cache modul setine duyarli olmali** (Faz-3.1) — TenantDbContextModelCacheKeyFactory eklendi. Test isolation problemi olarak ortaya cikti, ama Faz-5'te dinamik modul listesi senaryosunda zaten gerekecekti.

2. **Route token collision** (Faz-3.2) — `action` parametre adi MVC default route'unda `{action}` ile cakisti. `[FromQuery(Name="act")]` ile fix. Ders: parametre adlari MVC reserved kelimelerle (action, controller, area) cakismamali.

3. **EF Core MigrateAsync DB auto-create** (Faz-3.3) — non-existent DB MigrateAsync'te otomatik yaratiliyor. Fail simulasyonu icin bad credentials kullanmak gerekti.

4. **FindAsync query filter belirsizligi** (Faz-3.3) — EF Core 9'da FindAsync soft-deleted entity'yi buluyor mu belirsiz. Expression PK predicate + IgnoreQueryFilters ile kacindik. Bunu MODULE_CRUD_PATTERN.md "dokunma listesi"ne ekledik.

5. **PermissionService sealed** (Faz-3.4) — Test inherit edemedi. Decorator IPermissionService uzerinden gitti, sealed bozulmadi.

6. **MemoryCache post-eviction async** (Faz-3.4) — Callback async, test sync flaky. Polling ile cozuldu.

## Ertelemeler

- **Acilista:** 13 aktif
- **Kapatildi:** D-017 (audit transactional integrity), D-018 (tenant migration apply gap), D-012 (permission cache)
- **Dogdu:** D-019 (tenant migration fail UI gorunurluk)
- **Faz-3'ten Faz-6'ya kaydi:** D-002 (Hangfire MySQL) — audit ic-transaction cozumu Hangfire ihtiyacini elimine etti
- **Sonu:** 13 aktif (denge korundu)

## Faz-4'e Devir

- Faz-4: Media + SEO + Settings (3 cekirdek modul)
- Bu fazda gercek modul yazimi BASLAR — IHasEntities + IHasPermissions kullanimi ilk kez yapilir
- MODULE_CRUD_PATTERN.md kilavuz olarak kullanilacak; eksikleri Faz-4'te tamamlanir
- D-014 (IModule.RegisterServices DI integration no-op) Faz-5 tetigi — Faz-4 modulleri RegisterServices kullanmiyorsa Faz-4 sonuna ertelenebilir; modul DI servis ihtiyacinda Faz-4 icinde de cikabilir
- CachedPermissionService dormant durumda; Faz-4'te `[HasPermission]` kullanan ilk endpoint geldiginde canli HIT/MISS log gorulecek

## Olculer

- **Test sayisi:** Faz-2 sonu 85 → Faz-3 sonu 124 (+39, +45.9%)
- **Commit sayisi:** 10 commit (5 feat + 5 docs)
- **Build:** Release 0 warning / 0 error (her commit'te)
- **Format:** dotnet format temiz (her commit'te)
