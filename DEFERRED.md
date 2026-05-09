# DEFERRED.md — Ertelenen İşler

> Bu dosya, **şimdi yapılmıyor ama sonra yapılacak** işlerin canlı listesidir.
> Geçmiş kayıt değil — gelecek-bakışlı.
> Her adım başında okunur, sonunda güncellenir.

**Son güncelleme:** Faz-1.2 — D-003 ve D-004 eklendi.

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

### D-003 — Yeni proje sablonlari + CPM uyumsuzlugu
**Bağlam:** Faz-1.1'de "dotnet new xunit" Version'li PackageReference'lar uretti, CPM ile NU1008 catismasi cikti, csproj el ile yeniden yazildi. Yeni proje eklendiginde benzer sapma beklenmeli.
**Tetik:** Faz-2 veya sonrasi (yeni proje eklenecek faz)
**Eklenme:** Faz-1.2

### D-004 — JSON-based modul manifest pre-load discovery
**Bağlam:** ModuleManifest code-first record olarak duruyor, modul DLL'i yuklenmeden manifest okunamiyor. 10+ modul kullanan projeler icin ileride DLL yuklemeden once metadata okumayi (module.json) dusunebiliriz. Bugun erken optimizasyon.
**Tetik:** v2 (Faz-8 sonrasi backlog)
**Eklenme:** Faz-1.2

---

## Faz Bazlı Tetik Tablosu

| Tetik Faz | Bekleyen Madde Sayısı |
|---|---|
| Faz-1 | 1 (D-001) |
| Faz-2 | 1 (D-003) |
| Faz-3 | 1 (D-002, alternatif Faz-6) |
| Faz-4 | 0 |
| Faz-5 | 0 |
| Faz-6 | 0 |
| Faz-7 | 0 |
| Faz-8 | 0 |
| v2 | 1 (D-004) |

**Toplam aktif:** 4

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
