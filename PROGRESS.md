# PROGRESS.md — Mevcut Durum

> Bu dosya **canlı** durum dosyasıdır. Her adım sonunda güncellenir.
> Sade tutuyoruz; detaylı task listesi her fazın açılışında konuşulup üretilir.

**Son güncelleme:** Faz-1.2 (IModule kontrati ve manifest tipleri) — DONE

---

## Faz Yol Haritası

| Faz | Tema | Süre Hedefi | Durum |
|---|---|---|---|
| **Faz-0** | Repo + doküman seti kurulumu | 1 gün | 🟢 DONE |
| **Faz-1** | Çekirdek temel (IModule, ModuleLoader, Auth) | 2 hafta | 🟡 IN-PROGRESS |
| **Faz-2** | Multi-tenancy + RBAC | 2 hafta | ⚪ TODO |
| **Faz-3** | Generic CRUD + Audit | 2 hafta | ⚪ TODO |
| **Faz-4** | Media + SEO + Settings (3 çekirdek modül) | 2 hafta | ⚪ TODO |
| **Faz-5** | Blog modülü (full, referans modül) | 2 hafta | ⚪ TODO |
| **Faz-6** | Event Bus + Notification | 2 hafta | ⚪ TODO |
| **Faz-7** | Form Builder + Page Builder | 2 hafta | ⚪ TODO |
| **Faz-8** | Production hardening + ilk müşteri pilot | 2 hafta | ⚪ TODO |

**Durum işaretleri:** 🟢 DONE · 🟡 IN-PROGRESS · ⚪ TODO · 🔴 BLOCKED

---

## Aktif Faz: Faz-1 — Çekirdek temel

**Hedef:** IModule kontratini, ModuleLoader'i, Auth tablolari + login akisini hazirlamak. Solution iskeleti Faz-1.1'de kuruldu.

### Faz-1 Adımları

| Adım | Başlık | Durum |
|---|---|---|
| 1.1 | Solution iskeleti ve NuGet paketleri | 🟢 DONE |
| 1.2 | IModule kontrati (Cms.Abstractions) ve Manifest tipleri | 🟢 DONE |
| 1.3 | ModuleLoader (DLL discovery + reflection) | ⚪ TODO |
| 1.4 | Auth tablolari ve hashing | ⚪ TODO |
| 1.5 | Auth login/logout MVC controller | ⚪ TODO |

---

## Yapılanlar (kronolojik, en yeni üstte)

- **Faz-1.2** (commit: `c976f3b`): IModule + opsiyonel arayuzler (IHasEntities/Endpoints/Permissions/MenuItems) + ModuleBase + ModuleManifest/Dependency + Permission/Menu deger object'leri + 7 value object testi. Cms.Abstractions FrameworkReference Microsoft.AspNetCore.App ile baglandi.
- **Faz-1.1** (commit: `6896aef`): Solution iskeleti + 4 proje (Cms.Abstractions, Cms.Core, Cms.Web, Cms.Tests) + Central Package Management + Mediator/Pomelo/Serilog paketleri. Release build sifir warning, sifir error.
- **Faz-0.1-0.5** (commit: `e87ee2d`): Repo kurulum + dort dokuman yerlestirme + ilk push.

---

## Sıradaki

- **Faz-1.3:** ModuleLoader (DLL discovery + reflection) — modul DLL'lerini deploy/local/Modules/ altindan yukle, IModule implement edenleri kesfet, manifest dogrula.

---

## Sürüm ve Etiketler

> Her faz tamamlandığında git tag'i atılır: `v0.1.0` (Faz-1 sonu), `v0.2.0` (Faz-2 sonu)…
> Tag'ler henüz atılmadı.

---

## Notlar

- **Çalışma ritmi:** günde ~5 saat, haftada 5 gün, hafta sonu çalışma yok
- **Toplam hedef:** 16 hafta (Faz-1'den Faz-8'e)
- **Hedef GA:** Faz-8 sonu, ilk müşteri pilot deploy
