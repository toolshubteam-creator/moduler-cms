# DEFERRED.md — Ertelenen İşler

> Bu dosya, **şimdi yapılmıyor ama sonra yapılacak** işlerin canlı listesidir.
> Geçmiş kayıt değil — gelecek-bakışlı.
> Her adım başında okunur, sonunda güncellenir.

**Son güncelleme:** Faz-3.4 — D-012 kapatildi (PROGRESS Faz-3.4'e tasindi). Yeni erteleme yok.

---

## Format

Her madde şu yapıya uyar:

```
### [ID] — Madde başlığı
**Bağlam:** Neden ertelendi, hangi konuşmada/adımda doğdu (1-2 cümle)
**Tetik:** Hangi faz/adımda kapatılması bekleniyor
**Eklenme:** Hangi adımda eklendi
```

ID formatı: `D-001`, `D-002`... (sıralı, silinince ID tekrar kullanılmaz)

---

## Aktif Ertelemeler

### D-002 — Hangfire MySQL paket secimi
**Bağlam:** Hangfire.MySqlStorage ana paketinde concurrency bug'lari var; Hangfire.Storage.MySql forku index fix'leri ile geliyor. Hangi paketle gidilecegi background job kullanmaya basladigimizda kararlastirilmali.
**Tetik:** Faz-3 (Generic CRUD'da audit job'i icin) ya da Faz-6 (Event Bus + Notification)
**Eklenme:** Faz-1.1

### D-004 — JSON-based modul manifest pre-load discovery
**Bağlam:** ModuleManifest code-first record olarak duruyor, modul DLL'i yuklenmeden manifest okunamiyor. 10+ modul kullanan projeler icin ileride DLL yuklemeden once metadata okumayi (module.json) dusunebiliriz. Bugun erken optimizasyon.
**Tetik:** v2 (Faz-8 sonrasi backlog)
**Eklenme:** Faz-1.2

### D-014 — Modul IModule.RegisterServices DI integration
**Bağlam:** Faz-2.4a refactor'unda `BuildServiceProvider()` anti-pattern'i kaldirilirken `module.Instance.RegisterServices(services, configuration)` cagrisi da no-op kaldi (composition root build sonrasi modul yuklendigi icin DI'ye yeni servis ekleme penceresi kapaniyor). Modul yazimi Faz-5 (Blog modulu) ile basladiginda module'ler kendi servislerini DI'ye ekleyebilmeli. Cozum yollari: (a) `WebApplicationBuilder.UseCmsModules()` build oncesi modul DLL'lerini kesfedip RegisterServices'i normal DI registration phase'inde cagiran extension; (b) tenant-scoped servis kayit pattern'i (per-request service collection); (c) source generator + reflection ile compile-time module manifest.
**Tetik:** Faz-5 (Blog modulu — ilk gercek RegisterServices kullanimi)
**Eklenme:** Faz-2.4a

### D-015 — xUnit test parallelization
**Bağlam:** `[Collection(MySqlCollection.Name)]` pattern test class'larini sequential calistiriyor. DB-bagimli test sayisi arttikca calisma suresi lineer buyumeyor (Faz-2.3: 26 test → 9dk). Cozum: `MySqlContainerFixture`'i Collection yerine her test class icin `IClassFixture<>` ile ayri-ayri kullanmak (her class kendi container'ini ayaga kaldirir, paralel calisir) veya CollectionFixture'i statik singleton'a indirmek (DB'ler izole, container paylasimli, AMA xUnit class-paralel destekler hale getirmek). Risk: Docker Desktop'in es zamanli birden cok MySQL container'ini kararli yonetmemesi.
**Tetik:** Test suresi 15dk asarsa (mevcut trend Faz-2.4 sonrasi devam ederse)
**Eklenme:** Faz-2.4a

### D-006 — Module hot-reload + AssemblyLoadContext.Unload
**Bağlam:** Collectible context kuruldu ama unload mekanizmasi (admin panelinden modul kaldirma/yenileme) henuz yok. ModuleLoader.UnloadAsync veya ModuleManager service'i.
**Tetik:** Faz-8 (production hardening)
**Eklenme:** Faz-1.3

### D-007 — Account lockout + rate limiting
**Bağlam:** Faz-1.5'te login sade tutuldu. Production'da 5 hatali deneme sonrasi lockout, IP basina rate limit gerek.
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-1.5

### D-008 — Production setup wizard (ilk admin)
**Bağlam:** Dev'de SeedDevAdminAsync ile ilk admin olusur. Production'da bu calismaz; ilk kurulumda /setup wizard ya da CLI komutu gerek.
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-1.5

### D-009 — Cookie auth politika tuning
**Bağlam:** Default 14 gun sliding cookie, secure=auto. Production'da SameSite, Secure, ExpireTimeSpan, SlidingExpiration bilincli ayarlanmali.
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-1.5

### D-013 — Orphan permission cleanup admin UI
**Bağlam:** `PermissionSeeder` orphan permission'lari silmez (kullanici atamalari kaybolmasin diye). Modul kaldirildiktan sonra Sys_Permissions'ta kalan satirlari admin UI'dan temizleme akisi gerek (manuel onay + role atamalari raporu + bulk delete).
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-2.3

### D-016 — Production tenant DB provisioning credentials
**Bağlam:** Faz-2.4b'de uygulamanin DB user'i (`cms_dev`) tenant DB'lerini yaratabilmek icin CREATE/DROP ON *.* yetkisine sahip (development convenience). Production'da uygulama runtime user'i sinirli yetkili olmali; ayri bir admin user (orn. `cms_provisioner`) sadece DB lifecycle (`CREATE DATABASE`, `DROP DATABASE`, `GRANT`) icin kullanilmali ya da Hashicorp Vault / Azure Key Vault dinamik credentials. `ITenantProvisioningService` yapisi farkli connection string desteklemek icin halen acik (Connection options sub-section ayri).
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-2.4b

### D-011 — Tenant connection string encryption
**Bağlam:** `Sys_Tenants.ConnectionString` su an duz metin saklaniyor. Production'da DB'de SECRET_KEY ile sifrelenmeli, runtime'da decrypt edilip `TenantDbContextFactory.Create`'e gecirilmeli. Azure Key Vault, AWS Secrets Manager veya basit `IDataProtectionProvider` (ASP.NET Core dahili) entegrasyonu.
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-2.2

### D-010 — Login E2E test (Windows reverse-DNS quirk)
**Bağlam:** Faz-1.5'te `WebApplicationFactory<Program>` + Testcontainers MySQL kombinasyonu Windows host'ta `Host 'EXERT_2024_01' is not allowed` hatasi verdi. UserService unit testleri ayni container ile calisiyor — sorun Web host'un MySQL baglanti path'i ile sinirli, MySQL reverse-DNS Windows machine name'i `mysql.user` tablosundaki host eslesmesini bozuyor. 4 fix denendi (127.0.0.1 zorlama, --skip-name-resolve flag, manuel root GRANT, vs.) — tutmadi. Manuel UI ile login akisi (form, hatali parola, basarili login + cookie, logout) sirayla dogrulandi. AccountControllerLoginTests.cs silindi; Microsoft.AspNetCore.Mvc.Testing paketi ve Cms.Tests -> Cms.Web ProjectReference kaldi (Linux CI'da geri donecek).
**Tetik:** Linux CI/CD eklendiginde (Faz-7 production hardening) — Linux runner'da reverse-DNS sorunu yok, test direkt yesil donecek.
**Eklenme:** Faz-1.5

### D-019 — Tenant migration fail nedenini admin UI'da goster
**Bağlam:** Faz-3.3'te TenantMigrationRunner tek tenant fail'inin digerlerini durdurmamasi tasarlandi (D-018 cozumu) — fail durumlari ILogger ile log'a yazilir, admin UI sadece sayisal ozet (X/Y ok, Z fail) gosterir. Hangi tenant fail oldu, hangi exception, hangi timestamp? Admin perspektifinden gorunmuyor. Faz-3.3 manuel UI testinde acme tenant'in Faz-2.4 oncesi bozuk conn string'i fail oldu — runner durmadan devam etti (dogru davranis) ama admin "neden 1 fail?" sorusuna ancak log dosyasini acarak cevap bulur. Cozum secenekleri: (a) TempData'da fail detay listesi (gecici, redirect sonrasi temizlenir); (b) Sys_TenantMigrationLog tablosu (Timestamp, TenantId, Status, ErrorMessage; admin UI'da gecmis migration kayitlari sayfasi); (c) TenantMigrationReport extended (per-tenant FailureDetail listesi, view'da expandable). Production'da en degerlisi (b) — kalici denetim izi + raporlama. Dev'de (c) yeterli (log + tek seferlik gosterim).
**Tetik:** Faz-7 (production hardening — admin gorunurluk prod'da kritik, dev'de log yeterli)
**Eklenme:** Faz-3.3

---

## Faz Bazlı Tetik Tablosu

| Tetik Faz | Bekleyen Madde Sayısı |
|---|---|
| Faz-1 | 0 |
| Faz-2 | 0 |
| Faz-3 | 1 (D-002 alternatif Faz-6) |
| Faz-4 | 0 |
| Faz-5 | 1 (D-014) |
| Faz-6 | 0 |
| Faz-7 | 8 (D-007, D-008, D-009, D-010, D-011, D-013, D-016, D-019) |
| Faz-8 | 1 (D-006) |
| v2 | 1 (D-004) |
| Tetik: test suresi 15dk | 1 (D-015) |

**Toplam aktif:** 13

---

## Kapatılan Ertelemeler

> Buraya **silinmez**, kapatma sırasında PROGRESS.md "Yapılanlar" bölümüne taşınır.
> Bu bölüm sadece bir referans olarak boş kalır — gerçek arşiv PROGRESS.md'dedir.

### D-001 — Cms.Web → Cms.Modules.* referans guard
**Kapatildi:** Faz-1.5
**Cozum:** `src/Cms.Web/Directory.Build.targets` icine MSBuild target eklendi (`GuardAgainstModuleProjectReferences`). Cms.Web.csproj'a `Cms.Modules.*` prefix'li ProjectReference eklenmesi build-time'da hata firlatir. Negatif test ile dogrulandi (sahte `Cms.Modules.Fake` ref eklendi -> build fail; ref kaldirildi -> build yesil).

### D-005 — AddCmsModules icindeki BuildServiceProvider() anti-pattern
**Kapatildi:** Faz-2.4a
**Cozum:** `AddCmsModules` ikiye bolundu — `AddCmsModuleSystem` (DI kayitlari, build oncesi, servisleri ve `ModuleDescriptorRegistry` singleton'i ekler) + `LoadCmsModulesAsync` (build sonrasi `IHost` extension, modulleri yukleyip registry'ye yazar). `BuildServiceProvider()` cagrisi tamamen kaldirildi. `ModuleDescriptorRegistry` mutable holder pattern'i sayesinde DI'ye `IReadOnlyList<ModuleDescriptor>` sorgu zamani uretilebilir hale geldi (PermissionSeeder, TenantDbContextFactory, TenantDbContext bu list'i lazy resolve eder). Trade-off: `IModule.RegisterServices` cagrisi simdilik no-op — yeni D-014 olarak takipte.

---

## Notlar

- DEFERRED.md'ye **bug raporu** koyulmaz — bug'lar GitHub Issues'da takip edilir
- DEFERRED.md'ye **scope creep fikri** koyulur (sprint içinde "şunu da ekleyelim" diye gelen her şey)
- DEFERRED.md'ye **bilinçli teknik borç** kayıtlanır (Kural 7: borçlanma son çare)
- v2 backlog için tetik = `v2` yazılır; bu faz şu an tanımlı değil, Faz-8 sonrası planlanır
