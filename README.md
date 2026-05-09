# Modüler CMS

Müşteri projelerinde sıfırdan yazmak yerine modüler olarak hızlı kurulan, plug-in mimarili bir CMS altyapısı.

**Stack:** .NET 10 LTS · ASP.NET Core MVC · EF Core 9 (Pomelo) · MySQL 8 · Modular Monolith + Plugin Architecture

## Durum

🟡 Geliştirme aşamasında — Faz-0 (repo kurulumu).

İlerleme için: [PROGRESS.md](./PROGRESS.md)

## Doküman Seti

Bu repo, geliştirme süreci için dört zorunlu doküman ile yönetilir:

| Doküman | İçerik |
|---|---|
| [CLAUDE.md](./CLAUDE.md) | Proje anayasası — mimari kuralları, tech stack, hard rules |
| [WORKING_STYLE.md](./WORKING_STYLE.md) | İletişim ve süreç kuralları, komut formatı |
| [PROGRESS.md](./PROGRESS.md) | Mevcut durum, faz yol haritası |
| [DEFERRED.md](./DEFERRED.md) | Ertelenen işler (canlı liste) |

## Mimari Özet

- **Çekirdek (Cms.Core, Cms.Web)**: Auth, RBAC, multi-tenancy, plugin loader. Her projede aynı.
- **Modüller (Cms.Modules.*)**: Bağımsız DLL'ler. Runtime'da yüklenir. Çekirdek modülün adını bilmez.
- **Müşteri-özel kod (Cms.Customers.*)**: Tek müşteri için yazılmış DLL'ler.

Detaylı mimari dokümanı Faz-1'de `docs/ARCHITECTURE.md` olarak eklenecek.

## Geliştirme Ortamı

- .NET 10 SDK
- MySQL 8 (Docker veya yerel)
- VS Code + C# Dev Kit
- Claude Code v2.1.111+ (terminal-first iş akışı)

## Lisans

MIT — bkz. [LICENSE](./LICENSE)
