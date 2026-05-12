# FAZ-5-RETROSPECTIVE.md — Blog Modulu (Full, Referans Modul)

**Faz suresi:** Faz-5.1'den 5.3'e
**Faz tag:** v0.5.0
**Onceki tag:** v0.4.0 (Faz-4 sonu)

## Hedef vs Gerceklesen

**Hedef:** Faz-4'te oturan modul yazim pattern'inin gercek bir musteri-tipi modulle kanitlanmasi — Media + SEO + Settings entegrasyonlari dahil.

**Gerceklesen:** Hedef tam karsilandi. Uc alt-adim (5.1-5.3), uc kod commit + uc doc commit. 47 yeni test (156 → 203). Faz-5 acilisinda 12 erteleme, faz sonunda 12 (yeni acilim/kapanma yok, D-019 tekrar gozlemlendi ama mevcut kayit).

## Alt-Adim Ozeti

| Adim | Tema | Sonuc |
|---|---|---|
| 5.1 | Blog skeleton + Post entity + Media/SEO entegrasyonu | BlogPost (IAuditable + ISoftDeletable), slug uretimi, inline collapsible SEO, FeaturedMediaId raw FK + IMediaService validation — 17 test |
| 5.2 | Category + Tag + iliskiler + admin UI | Self-FK hierarchy, m-n junction'lar, cycle prevention, GetOrCreateManyAsync, indent emdash UI, N+1 batch fetch (erken cozum) — 25 test |
| 5.3 | Settings entegrasyonu + retrospektif + v0.5.0 | 5 setting key, IBlogSettingsReader snapshot pattern, /Blog/Settings dedike form, PostsController pagination, 2 yeni permission — 5 test |

## Tasarim Kararlari — Geri Bakis

### 1. Faz-4'te Comment ertelendi (Faz-5 acilisinda: Comment YOK)

Pattern kanitlamak icin Post+Category+Tag yeterli oldu. Comment moderation, spam korunma, nested reply gibi alt-sorular getirirdi — Faz-6/7'de daha dogal eslesir. Dogru karar.

### 2. Public-facing routes Faz-7'ye birakildi

Faz-5 admin-only kaldi. TenantResolutionMiddleware public path davranisi, anonim erisim, output cache, sitemap — bunlarin hepsi Faz-7 hardening'de tum modullerin public yuzu birlikte ele alinacak. Dogru karar.

### 3. AuthorUserId ayri sutun (IAuditable.CreatedBy degil)

IAuditable marker-only oldugu ortaya cikti (Faz-3.1) — zaten audit table'a yaziyor, entity'de field yok. AuthorUserId is kuralidir (yazar transferi, guest post), audit ayri kavram. Karar dogruydu.

### 4. FeaturedMediaId raw FK (navigation YOK)

CLAUDE.md Kural 5 ile uyumlu: modul ana projeleri arasi referans yok, sadece Contracts. View'da IMediaService runtime lookup ile cozuldu. Faz-4.3 SEO→Settings pattern'inin tekrari, sorunsuz tasindi.

### 5. Post-Category m-n (n-1 plani revize edildi)

Faz-5 acilisinda n-1 dusunulmustu; strateji turunda WordPress/Ghost/Strapi pratik ortusmesi ile m-n'e cevrildi. Faz-7'de "musteri coklu kategori istiyor" refactor borcundan kacinildi.

### 6. Tag free-text input (picker degil)

WordPress pattern; yazar UX'i icin standart. GetOrCreateManyAsync ile mevcut tag reuse + yeni tag auto-create. Karar dogruydu, manuel UI testte dogal hissetti.

### 7. Hierarchy gosterimi: indent emdash (Karar B)

Duz parent name kolonu yerine indent agacli — 3 seviye derinligi temiz okundu. Faz-7'de tam tree UI'a yukseltilebilir; simdiki yeterli.

### 8. Category delete-with-children: reject (cascade/orphan degil)

Yanlislikla agac silen kullaniciyi koruyan default. Cascade soft-delete restore karmasiklasacakti; orphan ise URL hierarchy iddiamizla celisiyordu. Karar dogruydu.

### 9. /Blog/Settings dedike form (generic key-value degil)

Faz-5.3 strateji turunda Karar B; donanimli musteri panel'i icin generic key-value tablo zayif UX'di. Dedike form numeric input + checkbox + validation ile temiz oldu.

### 10. IBlogSettingsReader snapshot pattern

ISettingsService cagrilarinin default fallback + range validation logic'ini modul-icinde kapsulleyen reader. Controller/view tarafi sade kaldi. Faz-6+ modulleri icin tekrarlanabilir pattern.

## Beklenmedik Bulgular

1. **DateTime precision MySQL datetime(6) round-trip** (Faz-5.1) — In-memory `DateTime.UtcNow` ticks > MySQL precision. UnpublishAsync test fail; baseline GetAsync DB round-trip ile cozuldu. Diger modullerde IAuditable timestamp testlerinde de ortaya cikabilir, MODULE_CRUD_PATTERN.md'ye not eklendi.

2. **`dotnet run --no-build` migration scaffold sonrasi stale binary** (Faz-5.2) — TenantMigrationRunner "success" loglasa da yeni migration runtime'da gorulmedi. Debug rebuild zorunlu. MODULE_CRUD_PATTERN.md dokunma listesinde.

3. **N+1 batch fetch erken cozum** (Faz-5.2) — Plan'da "Faz-7 perf'e ertelenir" demistim; PostService.ListAsync yazilirken batch fetch ile cozulmesi minor efort oldugundan simdi yapildi. Faz-7'de tasinmiyor.

4. **Pomelo self-FK + composite junction PK sorunsuz** (Faz-5.2) — Riskli noktaydi, EF Core 9 + Pomelo 9.0.0 kombosu beklendigi gibi calisti, manuel migration SQL duzeltmesi gerekmedi.

5. **D-019 tekrar gozlem (acme tenant fail UI'da gorunmedi)** (Faz-5.2 manuel UI) — Bilinen erteleme, Faz-7 tetik, ek aksiyon yok.

## Ertelemeler

- **Acilista:** 12 aktif (Faz-4 sonu)
- **Kapatildi:** 0
- **Dogdu:** 0
- **Sonu:** 12 aktif (denge korundu)
- **D-002 (Hangfire MySQL)** Faz-6 tetik; Blog'da PublishAt sutunu hazir bekliyor (scheduled publish), Faz-6'da iki gereksinim birlestirilebilir.
- **D-019 (tenant migration fail UI gorunurluk)** Faz-7 tetik, bu fazda iki kez gozlemlendi (5.2 + 5.3 manuel UI), kayit zaten var.

## Faz-6'ya Devir

- **Faz-6: Event Bus + Notification + Hangfire** (PROGRESS.md plan'a gore).
- **D-002 burada cozulecek:** Hangfire.MySqlStorage vs Hangfire.Storage.MySql fork karari.
- **Blog scheduled publish** (PublishAt → background job ile auto-publish) Hangfire ile birlikte gelir; ayri faz icine gerek yok.
- **Modul yazim pattern'i kanitli:** Cross-module Contracts referansi (Media + SEO + Settings), IBlogSettingsReader snapshot, self-FK hierarchy, m-n junction, free-text auto-create — tum desenler MODULE_CRUD_PATTERN.md'de gunceldir.

## Olculer

- **Test sayisi:** Faz-4 sonu 156 → Faz-5 sonu 203 (+47, +30.1%)
- **Commit sayisi:** 6 commit (3 feat + 3 docs; 5.3'te retrospektif + tag)
- **Build:** Release 0 warning / 0 error (her commit'te)
- **Format:** dotnet format temiz (her commit'te)
- **Modul:** 3 (settings, media, seo) → 4 (+ blog)
- **Permission:** 13 → 15 (blog modulunde toplam; sistem genelinde Settings/Media/SEO + Blog)
- **Tablo:** Tenant DB'de Blog_Posts, Blog_Categories, Blog_Tags, Blog_PostCategories, Blog_PostTags (5 yeni)
- **Aktif erteleme:** 12 → 12 (denge)
