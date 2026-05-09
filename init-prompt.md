# Yeni Sohbet Açılış Prompt'u

> Bu dosya, yeni bir Claude.ai sohbeti veya yeni bir Claude Code oturumu açtığınızda kullanacağın hazır prompt şablonunu içerir.

---

## Senaryo 1: Claude.ai Web Sohbeti (Tarayıcı)

Yeni bir Claude.ai sohbeti aç ve aşağıdaki metni **olduğu gibi** ilk mesaj olarak gönder. Repo public olduğu için Claude dokümanları doğrudan GitHub'dan okuyacaktır.

```
Modüler CMS projesinde geliştirici olarak çalışıyorum. Solo developer'ım,
.NET 10 + MySQL + ASP.NET Core MVC kullanıyorum, IDE'm VS Code + Claude Code.

Repo: https://github.com/<KULLANICI_ADIN>/<REPO_ADI>

Bu repo'daki **dört zorunlu dokümanı** sırayla okuyup özümse:

1. https://github.com/<KULLANICI_ADIN>/<REPO_ADI>/blob/main/CLAUDE.md
2. https://github.com/<KULLANICI_ADIN>/<REPO_ADI>/blob/main/WORKING_STYLE.md
3. https://github.com/<KULLANICI_ADIN>/<REPO_ADI>/blob/main/PROGRESS.md
4. https://github.com/<KULLANICI_ADIN>/<REPO_ADI>/blob/main/DEFERRED.md

Okuma bittikten sonra WORKING_STYLE.md'deki "YENİ KONUŞMA BAŞLANGICI"
checklist'ini sırayla uygula:

1. Mevcut durum: hangi adım tamam, hangisi sıradaki?
2. Çalışma ortamı: OS, shell, runner — sor
3. Dört dokümanın okunduğunu kısa özetle teyit et

Sonra DOĞRUDAN KOMUT VERME — önce strateji konuşalım.
PROGRESS.md'deki sıradaki adım için kapsam, risk ve alternatifleri tartış.
```

`<KULLANICI_ADIN>` ve `<REPO_ADI>` yerlerini gerçek değerlerle doldur.

---

## Senaryo 2: Claude Code Terminal Oturumu

Lokal repo klasöründe terminal aç:

```bash
cd ~/projects/<REPO_ADI>
claude
```

Claude Code zaten CLAUDE.md'yi otomatik okuyacak. Açılış mesajı olarak şunu yaz:

```
Yeni bir oturum başlatıyorum. Repo kökündeki dört zorunlu dokümanı oku:
CLAUDE.md, WORKING_STYLE.md, PROGRESS.md, DEFERRED.md.

Okuma bittikten sonra WORKING_STYLE.md'deki "YENİ KONUŞMA BAŞLANGICI"
checklist'ini uygula:

1. Mevcut durum: PROGRESS.md'den hangi adım tamam, hangisi sıradaki?
2. Çalışma ortamım: bana sor (OS, shell)
3. DEFERRED.md'de sıradaki adımda kapatılabilecek madde var mı kontrol et

Doğrudan komut verme — önce sıradaki adım için strateji konuşalım.
```

---

## Senaryo 3: Yarım Kalan Oturuma Devam

Aynı sohbete birkaç saat sonra geri döndüğünde veya `/clear` sonrası:

```
Devam ediyoruz. PROGRESS.md ve DEFERRED.md'yi yeniden oku, son durumu özetle.
WORKING_STYLE.md kurallarına göre devam et — kısa, net, doğrudan.

Sıradaki adım: <FAZ-X.Y - başlığı>
```

---

## Önemli Hatırlatmalar

- **Tarayıcıda Claude.ai kullanırken:** her yeni sohbet sıfırdan başlar, doküman dörtlüsünü her seferinde okumalı.
- **Claude Code terminal:** `~/.claude/sessions/` altında oturum hafızası tutulur ama CLAUDE.md her zaman yeniden okunur. WORKING_STYLE.md'yi açıkça hatırlatman gerekebilir.
- **Repo public** olduğu için web_fetch ile doğrudan erişim mümkün — kopyala-yapıştır gerekmez.
- **Repo private yaptığında:** dokümanları ya kopyala-yapıştır, ya da Claude'a GitHub OAuth tokenı ile erişim ver (önerilmez, security riski).

---

## Slash Command Önerileri (ileride)

Claude Code v2.1+ ile `.claude/skills/` altına yerleştirilen skill'ler `/komut` olarak çalışır. İlerideki fazlarda eklenecekler:

- `/faz-rapor` — Mevcut faz için özet rapor üret
- `/faz-retro` — Faz sonu retrospektif yaz
- `/yeni-modul <ad>` — Yeni modül iskelesi (Faz-5'ten sonra anlamlı)
- `/test-tum` — Tüm testleri çalıştır, başarısızları özetle
- `/commit-faz <X.Y> <mesaj>` — Standart commit format

Bu skill'ler Faz-1'in son adımında oluşturulacak.
