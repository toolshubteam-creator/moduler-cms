# FAZ-4-RETROSPECTIVE.md — Media + SEO + Settings (3 Cekirdek Modul)

**Faz suresi:** Faz-4.1'den 4.4'e
**Faz tag:** v0.4.0
**Onceki tag:** v0.3.0 (Faz-3 sonu)

## Hedef vs Gerceklesen

**Hedef:** Ilk gercek modul yazimi — modul infrastructure pipeline'inin kanitlanmasi + uc cekirdek modul (Settings, Media, SEO) full implementasyon.

**Gerceklesen:** Hedef tam karsilandi. Dort alt-adim (4.1-4.4), uc kod commit + dort doc commit (4.4 sadece doc + tag). 32 yeni test (124 → 156). Faz-4 acilisinda 13 erteleme, faz sonunda 12 (D-014 kapatildi, yeni erteleme dogmadi). Modul infrastructure'inin kritik fix'leri (DLL copy glob, ALC type identity x2) Faz-4 icinde ortaya cikip kapandi — Faz-5 Blog modulu temiz pattern'e basliyor.

## Alt-Adim Ozeti

| Adim | Tema | Sonuc |
|---|---|---|
| 4.1 | Modul infra + Settings modulu | Directory.Build.targets DLL copy, UseCmsModules RegisterServices, ISettingsService DI, 10 test, D-014 kapandi |
| 4.2 | Media modulu | IFileStorage abstraction + LocalDiskFileStorage, SHA256 dedup (B yaklasimi), ISoftDeletable, 10 test |
| 4.3 | SEO modulu | TargetType+TargetId generic binding, ResolveAsync Settings fallback (ilk cross-module Contracts kullanimi), SeoMetaTagHelper, 12 test |
| 4.4 | Retrospektif + v0.4.0 tag | Bu dosya |

## Tasarim Kararlari — Geri Bakis

### 1. Vertical slice vs horizontal infra (Faz-4 acilisinda: A secildi)

**Secim:** Vertical slice — Settings (infra + modul ardarda), sonra Media, sonra SEO.

**Geri bakis:** Dogru karar. 4.1 sonunda calisir bir modul vardi; infrastructure'in dogru calistigi gercek bir modul ile dogrulandi. Horizontal yaklasimda "bos manifest smoke test" alt-adimi sentetik kalacakti. 4.1'in genis kapsami (modul infra + Settings full) endise yaratmisti — pratikte tek alt-adimda toplandi, 10 test yesil.

### 2. Modul Contracts + ana proje cifti (Faz-4 acilisinda: B secildi)

**Secim:** Her modul icin Cms.Modules.X + Cms.Modules.X.Contracts cifti baska modul referansi yokken bile.

**Geri bakis:** Isabet. Faz-4.3'te SEO → Settings.Contracts referansi geldiginde hazirdi; Contracts'i refactor turu ile sonradan eklemek gerekmedi. Kural 5'in pratik karsiligi.

### 3. Migration ortak klasor + naming (Faz-4 acilisinda: C secildi)

**Secim:** Tek `Data/Migrations/Tenant/` klasoru, `Yyyymmdd_ModuleId_Description` naming disiplini.

**Geri bakis:** Sade ve isleyen. Uc modulun migration'lari ardarda yazildi, isim cakismasi olmadi. EF Core multi-assembly migrations kompleksitesinden kacinildi. Modul kaldirma temizligi Faz-8 (D-006) ile birlikte tartisilacak.

### 4. Media dedup B yaklasimi (Faz-4.2: B secildi)

**Secim:** Disk'te tek dosya (hash bazli), DB'de ayri metadata satirlari.

**Geri bakis:** Dogru. Manuel UI testinde ayni dosya iki kez yuklendiginde 2 DB satir + 1 fiziksel dosya gozlemlendi. UX karisikligi yok (her upload kendi metadata'sina sahip), disk tasarrufu var. S3'e gecis Faz-7+'ta IFileStorage interface ile temiz olacak.

### 5. SEO generic target binding (Faz-4.3: A secildi)

**Secim:** TargetType (string) + TargetId (string) — SEO modulu hicbir hedef tipinin adini bilmez.

**Geri bakis:** Kural 1 ile uyumlu. URL-based alternatife gore daha esnek (URL degisikliginde SEO bagi korunur). Faz-5 Blog modulu `("blog.post", postId)` ile kullanacak.

## Beklenmedik Bulgular

1. **DLL copy plan'da eksikti** (Faz-4.1) — Plan'daki `Copy($(TargetPath))` sadece ana DLL'i kopyaladi; Contracts.dll + transitive paketler + .deps.json eksikti, AssemblyDependencyResolver runtime'da fail etti. Cozum: `$(TargetDir)*.dll + $(TargetDir)*.deps.json` glob. Modul DLL deployment'in "tek dosya degil paket" oldugu kalibre noktasi.

2. **ModuleLoadContext type identity bug (NuGet.Versioning)** (Faz-4.1) — Default ALC'de henuz yuklenmemis paketler ModuleLoadContext'te ayri kopya olarak yuklendi, type identity bozuldu (NuGetVersion@Module ≠ NuGetVersion@Default → MissingMethodException). Cozum: `Default.LoadFromAssemblyName(name)` ile proaktif probing — modul ALC null donerse Default ALC'den paylasilir. Faz-5+'ta modul sayisi artarken proaktif kalibre noktasi.

3. **Cross-module Contracts type identity bug** (Faz-4.3) — Bulgu #2'nin Contracts genelinde tekrari. Iki modul ayni Contracts'in ayri kopyalarini yukledi, ISettingsService DI build'de "service not found" exception. Cozum: `Cms.Modules.*.Contracts.dll` glob'unu host start'inda eagerly Default ALC'ye yukle. Bulgu #2'nin stratejik genislemesi.

4. **Areas route 2+ segment zorunlulugu** (Faz-4.1) — Default areas route pattern `{area:exists}/{controller}/{action}` tek-segment URL'i (`/Settings`) eslemiyor. `/Settings/Settings`, `/Media/Files`, `/Seo/Metas` `/{Area}/{Controller}` formatinda. `/Admin/Audit` ile simetrik, MODULE_CRUD_PATTERN.md'ye kayit.

5. **appsettings.json'da Modules:Path relative path** (Faz-4.1) — `dotnet run --project src/Cms.Web` cwd'si source folder oldugu icin relative "Modules" cozumlenemedi. ModuleLoaderOptions default initializer `AppContext.BaseDirectory/Modules` mutlak path ile cozuldu. Config field gerek olmadigi anlasildi, silindi.

## Ertelemeler

- **Acilista:** 13 aktif (Faz-3 sonu)
- **Kapatildi:** D-014 (IModule.RegisterServices DI integration)
- **Dogdu:** 0
- **Sonu:** 12 aktif

## Faz-5'e Devir

- Faz-5: Blog modulu (full, referans modul).
- Modul yazim pattern'i artik kanitli: Contracts + ana proje cifti, IHasEntities + IHasPermissions, area route `/Area/Controller`, IAuditable/ISoftDeletable marker'lari, MODULE_CRUD_PATTERN.md gunceldir.
- Blog modulu Faz-4'un uc moduluyle de etkilesir: Media (post.featured_image), SEO (post per-record meta), Settings (default URL pattern).
- ALC + Contracts deployment kanali stabil — yeni modul eklerken DLL copy/identity sorunu beklenmiyor.
- D-002 (Hangfire MySQL) Faz-6'da bekliyor — Blog modulu icinde scheduled publish gibi job ihtiyaci cikarsa Faz-5 icinde de gelebilir.
- CachedPermissionService Faz-4'te uc modulun [HasPermission] kullanimi ile aktif route'larda calisti — Faz-5'te kanitlanmis.

## Olculer

- **Test sayisi:** Faz-3 sonu 124 → Faz-4 sonu 156 (+32, +25.8%)
- **Commit sayisi:** 7 commit (3 feat + 4 docs; 4.4 sadece doc + tag)
- **Build:** Release 0 warning / 0 error (her commit'te)
- **Format:** dotnet format temiz (her commit'te)
- **Modul:** 0 → 3 (settings, media, seo — hepsi core)
- **Aktif erteleme:** 13 → 12
