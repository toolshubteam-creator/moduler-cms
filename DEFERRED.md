# DEFERRED.md — Ertelenen İşler

> Bu dosya, **şimdi yapılmıyor ama sonra yapılacak** işlerin canlı listesidir.
> Geçmiş kayıt değil — gelecek-bakışlı.
> Her adım başında okunur, sonunda güncellenir.

**Son güncelleme:** Faz-2.2 — D-011 eklendi (Tenant connection string encryption, Faz-7 tetigi).

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

### D-005 — AddCmsModules icindeki BuildServiceProvider() anti-pattern
**Bağlam:** Module discovery DI tamamlanmadan once gerekli oldugu icin ServiceCollection.BuildServiceProvider() cagriliyor (multiple service providers). Daha temiz: HostBuilder extension veya StartupTask pattern.
**Tetik:** Faz-2 (Multi-tenancy + RBAC kurarken composition root yenilenecek)
**Eklenme:** Faz-1.3

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

### D-011 — Tenant connection string encryption
**Bağlam:** `Sys_Tenants.ConnectionString` su an duz metin saklaniyor. Production'da DB'de SECRET_KEY ile sifrelenmeli, runtime'da decrypt edilip `TenantDbContextFactory.Create`'e gecirilmeli. Azure Key Vault, AWS Secrets Manager veya basit `IDataProtectionProvider` (ASP.NET Core dahili) entegrasyonu.
**Tetik:** Faz-7 (production hardening)
**Eklenme:** Faz-2.2

### D-010 — Login E2E test (Windows reverse-DNS quirk)
**Bağlam:** Faz-1.5'te `WebApplicationFactory<Program>` + Testcontainers MySQL kombinasyonu Windows host'ta `Host 'EXERT_2024_01' is not allowed` hatasi verdi. UserService unit testleri ayni container ile calisiyor — sorun Web host'un MySQL baglanti path'i ile sinirli, MySQL reverse-DNS Windows machine name'i `mysql.user` tablosundaki host eslesmesini bozuyor. 4 fix denendi (127.0.0.1 zorlama, --skip-name-resolve flag, manuel root GRANT, vs.) — tutmadi. Manuel UI ile login akisi (form, hatali parola, basarili login + cookie, logout) sirayla dogrulandi. AccountControllerLoginTests.cs silindi; Microsoft.AspNetCore.Mvc.Testing paketi ve Cms.Tests -> Cms.Web ProjectReference kaldi (Linux CI'da geri donecek).
**Tetik:** Linux CI/CD eklendiginde (Faz-7 production hardening) — Linux runner'da reverse-DNS sorunu yok, test direkt yesil donecek.
**Eklenme:** Faz-1.5

---

## Faz Bazlı Tetik Tablosu

| Tetik Faz | Bekleyen Madde Sayısı |
|---|---|
| Faz-1 | 0 |
| Faz-2 | 1 (D-005) |
| Faz-3 | 1 (D-002, alternatif Faz-6) |
| Faz-4 | 0 |
| Faz-5 | 0 |
| Faz-6 | 0 |
| Faz-7 | 5 (D-007, D-008, D-009, D-010, D-011) |
| Faz-8 | 1 (D-006) |
| v2 | 1 (D-004) |

**Toplam aktif:** 9

---

## Kapatılan Ertelemeler

> Buraya **silinmez**, kapatma sırasında PROGRESS.md "Yapılanlar" bölümüne taşınır.
> Bu bölüm sadece bir referans olarak boş kalır — gerçek arşiv PROGRESS.md'dedir.

### D-001 — Cms.Web → Cms.Modules.* referans guard
**Kapatildi:** Faz-1.5
**Cozum:** `src/Cms.Web/Directory.Build.targets` icine MSBuild target eklendi (`GuardAgainstModuleProjectReferences`). Cms.Web.csproj'a `Cms.Modules.*` prefix'li ProjectReference eklenmesi build-time'da hata firlatir. Negatif test ile dogrulandi (sahte `Cms.Modules.Fake` ref eklendi -> build fail; ref kaldirildi -> build yesil).

---

## Notlar

- DEFERRED.md'ye **bug raporu** koyulmaz — bug'lar GitHub Issues'da takip edilir
- DEFERRED.md'ye **scope creep fikri** koyulur (sprint içinde "şunu da ekleyelim" diye gelen her şey)
- DEFERRED.md'ye **bilinçli teknik borç** kayıtlanır (Kural 7: borçlanma son çare)
- v2 backlog için tetik = `v2` yazılır; bu faz şu an tanımlı değil, Faz-8 sonrası planlanır
