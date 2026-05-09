# DEFERRED.md — Ertelenen İşler

> Bu dosya, **şimdi yapılmıyor ama sonra yapılacak** işlerin canlı listesidir.
> Geçmiş kayıt değil — gelecek-bakışlı.
> Her adım başında okunur, sonunda güncellenir.

**Son güncelleme:** Faz-1.3 — D-003 kapatildi (manuel csproj yazimi), D-005 ve D-006 eklendi.

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

### D-001 — Cms.Web → Cms.Modules.* referans guard
**Bağlam:** Kural 2 derleme zamaninda otomatik enforce edilmiyor; MSBuild target ile referans engelleyici eklenmeli.
**Tetik:** Faz-1.3 (ModuleLoader yazilirken)
**Eklenme:** Faz-1.1

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

---

## Faz Bazlı Tetik Tablosu

| Tetik Faz | Bekleyen Madde Sayısı |
|---|---|
| Faz-1 | 1 (D-001) |
| Faz-2 | 1 (D-005) |
| Faz-3 | 1 (D-002, alternatif Faz-6) |
| Faz-4 | 0 |
| Faz-5 | 0 |
| Faz-6 | 0 |
| Faz-7 | 0 |
| Faz-8 | 1 (D-006) |
| v2 | 1 (D-004) |

**Toplam aktif:** 5

---

## Kapatılan Ertelemeler

> Buraya **silinmez**, kapatma sırasında PROGRESS.md "Yapılanlar" bölümüne taşınır.
> Bu bölüm sadece bir referans olarak boş kalır — gerçek arşiv PROGRESS.md'dedir.

---

## Notlar

- DEFERRED.md'ye **bug raporu** koyulmaz — bug'lar GitHub Issues'da takip edilir
- DEFERRED.md'ye **scope creep fikri** koyulur (sprint içinde "şunu da ekleyelim" diye gelen her şey)
- DEFERRED.md'ye **bilinçli teknik borç** kayıtlanır (Kural 7: borçlanma son çare)
- v2 backlog için tetik = `v2` yazılır; bu faz şu an tanımlı değil, Faz-8 sonrası planlanır
