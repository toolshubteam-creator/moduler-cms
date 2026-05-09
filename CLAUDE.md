# CLAUDE.md — Proje Anayasası

> Bu dosya projenin **mimari ve teknik kurallarını** tanımlar.
> İletişim ve süreç kuralları için **WORKING_STYLE.md**'ye bakın.
> Mevcut durum için **PROGRESS.md**'ye, ertelenen işler için **DEFERRED.md**'ye bakın.

## Proje Misyonu

Müşteri projelerinde sıfırdan yazmak yerine modüler olarak hızlı kurulan, plug-in mimarili bir CMS. Her yeni müşteri projesi için aynı kod tabanı kullanılır; modüller runtime'da DLL olarak yüklenir; müşteriye özel ihtiyaçlar müşteri-spesifik modül DLL'leri ile karşılanır.

**Başarı kriteri:** Yeni bir müşteri talebi geldiğinde, mevcut modüllerden seçim yaparak veya yeni bir modül DLL'i yazarak — çekirdeği değiştirmeden — proje teslim edebilmek.

## Tech Stack (PIN — değiştirme önerme)

| Katman | Seçim | Sürüm | Notlar |
|---|---|---|---|
| Framework | .NET LTS | net10.0 | Kasım 2028'e kadar destekli |
| Dil | C# | 14 | nullable enabled, ImplicitUsings enabled |
| Web | ASP.NET Core | 10.0.x | MVC + Minimal API hibrit |
| ORM | EF Core | 9.0.0 | Pomelo .NET 10 / EF Core 10'u henüz tam desteklemiyor |
| MySQL Provider | Pomelo.EntityFrameworkCore.MySql | 9.0.0 | EF Core 10'a geçiş Pomelo stabilize olunca |
| DB | MySQL | 8.x | utf8mb4_0900_ai_ci collation |
| Mediator | Mediator (martinothamar) | 3.0.2 | Source-generator, MediatR'den lisans nedeniyle gecildi |
| Validation | FluentValidation | 11.10.x | Attribute kirliliği yok |
| Mapping | Mapster | 7.4.x | AutoMapper YERİNE |
| Logging | Serilog.AspNetCore | 10.0.x | Console + File sink, .NET 10 hizali |
| Background | Hangfire + Hangfire.MySqlStorage | 1.8.x / 2.0.x | Job dashboard /admin/jobs |
| Test | xUnit + FluentAssertions + Testcontainers | latest | Gerçek MySQL ile entegrasyon testi |
| IDE | VS Code + C# Dev Kit | latest | Visual Studio 2022/2026 değil |
| AI Asistanı | Claude Code | v2.1.111+ | Terminal-first iş akışı |

## Mimari Kuralları (HARD — bozma)

### Kural 1: Çekirdek hiçbir modülün adını bilmez

`Cms.Core` ve `Cms.Web` projelerinde `BlogModule`, `ECommerceModule` gibi modül adları **asla** geçmez. Çekirdek sadece `IModule` interface'ini tanır.

```csharp
// YANLIŞ
if (module is BlogModule blog) { ... }

// DOĞRU
foreach (var module in _modules) {
    module.RegisterServices(services, config);
}
```

### Kural 2: Cms.Web modüllere doğrudan referans veremez

Modüller runtime'da DLL olarak yüklenir, derleme zamanında değil.

```bash
# YASAK
dotnet add src/Cms.Web/Cms.Web.csproj reference src/Modules/Cms.Modules.Blog/Cms.Modules.Blog.csproj
```

### Kural 3: Her modül IModule implement eder

Bir modül kendini şu yöntemlerle tanıtır:
- `Manifest` — kimliği, sürümü, bağımlılıkları
- `RegisterServices` — DI kayıtları
- `RegisterEntities` — EF Core entity konfigürasyonları
- `MapEndpoints` — route'ları
- `GetPermissions` — izinleri
- `GetMenuItems` — sidebar öğeleri
- `OnInstallAsync` / `OnUninstallAsync` — yaşam döngüsü hook'ları

### Kural 4: Tablo isimleri modül prefix'i ile başlar

```
Blog_Posts, Blog_Categories, Blog_Tags
ECommerce_Orders, ECommerce_Products
CRM_Leads, CRM_Contacts
```

Çekirdek tablolar prefix kullanmaz: `Users`, `Roles`, `Tenants`.

### Kural 5: Modüller arası iletişim

- **Olay yayını** (asenkron): MediatR `IIntegrationEvent` üzerinden
- **Bilgi sorgusu** (senkron): Hedef modülün `Cms.Modules.X.Contracts` projesindeki interface'i üzerinden
- **YASAK:** Modül A → Modül B'nin asıl projesine doğrudan referans

### Kural 6: Multi-tenant izolasyon

- **Master DB** (`cms_master`): Tenants, TenantModules, kullanıcı kök bilgileri
- **Tenant DB** (her müşteri için ayrı): İş verileri
- Tenant resolver subdomain bazlıdır: `panel.acme.local` → `acme` tenant'ı

### Kural 7: Sıfır warning, sıfır hardcoded string

- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (Directory.Build.props)
- Hardcoded string yerine `const`, `enum` veya `Resources` kullan
- İstisna: log mesajları, exception mesajları

### Kural 8: Modul yukleme konvansiyonlari

- Modul DLL'leri `bin/.../Modules/` (default) veya `Modules:Path` config ile belirlenen dizinden yuklenir
- Her modul DLL'i bir `IModule` implementasyonu icermelidir (yoksa atlanir, hata degil)
- Modul ASP.NET Core, EF Core, Cms.Abstractions tiplerini host'tan paylasir; kendi kopyasini DLL'inde tasimaz
- AssemblyLoadContext collectible — runtime'da modul kaldirilabilir
- Modul yukleme sirasi: IsCorePlugin=true olanlar once, sonra topological sort (Manifest.Dependencies'e gore)
- Cycle veya cozumlenemeyen dependency tum yuklemeyi durdurur

### Kural 9: Auth ve veri modeli

- Auth tablolari Master DB'de (`Sys_` prefix: `Sys_Users`, `Sys_Roles`, `Sys_UserRoles`, `Sys_Permissions`, `Sys_RolePermissions`, `Sys_Tenants`)
- Multi-tenancy: `Sys_UserRoles` surrogate int `Id` PK + `(UserId, RoleId, TenantId)` UNIQUE INDEX. `TenantId` nullable; null = global rol, dolu = tenant-spesifik rol. MySQL'de NULL ≠ NULL semantigi unique index'i bozmaz (ayni user ayni role'u birden cok kez global atayamaz, ama global + tenant-A + tenant-B bir arada olabilir).
- `Tenant.Id` Guid (subdomain/path'te gorunecek), digerleri int auto-increment
- Sifre: PBKDF2-SHA256, 100k iter, 16 byte salt, 32 byte hash, format `"iter.salt_b64.hash_b64"`
- ASP.NET Core Identity KULLANILMIYOR; kendi `IUserService` ve `IPasswordHasher`
- Cookie auth: `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)`; Login/Logout/AccessDenied path'leri `/Account/Login` ve `/Account/Logout`. Production cookie politikalari (SameSite, Secure, ExpireTimeSpan) Faz-7'de tunelenecek.
- Dev admin seed: `Auth:DefaultAdmin` config'i ile yalniz `IsDevelopment()` iken calisir. Production'da bu seed kapanir; ilk admin /setup wizard ile yaratilacak (Faz-7).

## Code Style

- File-scoped namespace (`namespace X;` — kıvırcık parantez yok)
- Async tüm I/O metodları, `Async` suffix
- DTO'lar `record`, entity'ler `class`
- Primary constructors uygun yerlerde (C# 12+)
- LINQ `for` loop yerine, okunabilirse
- Nullable annotation'ları açık (`string?`, `int?`)
- `var` kullan, type aşikarsa
- xUnit + FluentAssertions: `result.Should().Be(expected)` formatı

## Build Konvansiyonu

- TreatWarningsAsErrors yalnizca Release konfigurasyonunda etkin
- CI/PR ve commit oncesi: `dotnet build -c Release` ile dogrula
- Yerel gelistirme Debug'da serbest (analyzer noise'i sadece Release'de hata)
- AnalysisMode: Default (correctness-oncelikli; performance-oncelikli kurallar suggestion seviyesinde, tek tek opt-in)
- EF Core migration dosyalari `Data/Migrations/` altinda block-style namespace kullanir; `.editorconfig` IDE0161'i bu klasor icin gevsetir. Auto-generated dosyalari **manuel duzeltme** — sadece migration adimi sirasinda gercek hatayi (FK drop sirasi, vb.) duzeltmek icin elle dokunulur.
- `src/Cms.Web/Directory.Build.targets` Cms.Web.csproj'a `Cms.Modules.*` prefix'li ProjectReference eklenmesini build-time'da reddeder (CLAUDE.md Kural 1, Kural 2 enforcement). Kural 4'teki tablo prefix konvansiyonuna paralel.

## Komutlar

| Amaç | Komut |
|---|---|
| Build | `dotnet build` |
| Test | `dotnet test` |
| Run | `dotnet run --project src/Cms.Web` |
| Run (modulsuz) | `dotnet run --project src/Cms.Web` |
| Format | `dotnet format` |
| Migration ekle (master) | `dotnet ef migrations add <Name> --project src/Cms.Core --startup-project src/Cms.Web --output-dir Data/Migrations` |
| Migration uygula | `dotnet ef database update --project src/Cms.Core --startup-project src/Cms.Web` |
| Default admin (Dev) | `appsettings.Development.json` -> `Auth:DefaultAdmin` (Email/Password). Sadece IsDevelopment() iken seed olur. |

## Solution Dosyasi

- `Cms.slnx` (XML, .NET 10 default formati)
- VS Code C# Dev Kit'te otomatik bulunur; bulamazsa `.vscode/settings.json`'a `"dotnet.defaultSolution": "Cms.slnx"` eklenebilir

## Don'ts

- **Microsoft.AspNetCore.Identity** kullanma — kendi Auth tablomuz var
- **AutoMapper** önerme — Mapster kullanıyoruz
- **dynamic** keyword'ü kullanma
- **PostgreSQL veya başka DB'ye geçiş** önerme — MySQL fix
- **Genel `Exception` catch** etme, rethrow veya log etmeden
- **EF Core 10'a özgü** sözdizimi kullanma (Pomelo henüz desteklemiyor)
- **Microsoft.AspNetCore.SignalR** ekleme (henüz scope dışı)
- **GraphQL kütüphanesi** ekleme (REST + minimal API yeterli)
- **MediatR** kullanma — Mediator (martinothamar) kullaniyoruz. MediatR v13+ ticari lisansli, v12 arsivlendi.
- **Cms.Abstractions framework-bagimsiz degildir** — ASP.NET Core ve EF Core abstractions'a kasitli baglidir. Baska web framework'une tasima plani yoktur.
- **`appsettings.Development.json`'i COMMIT ETME** — DB sifresi icerir, `.gitignore`'da. Repo'da sadece `appsettings.json` (bos `ConnectionStrings:Master`) bulunur.

## Definition of Done (her adım için)

Bir adım tamamlandı sayılır:

- [ ] Kod derleniyor, sıfır warning
- [ ] Test eklendi (veya neden eklenmediği açıkça gerekçelendirildi)
- [ ] `dotnet format --verify-no-changes` geçiyor
- [ ] Manuel doğrulama yapıldı (gerekiyorsa screenshot)
- [ ] PROGRESS.md güncellendi
- [ ] DEFERRED.md güncellendi (yeni erteleme/kapatılan)
- [ ] Commit atıldı, format: `feat: faz-X.Y kisa aciklama` (diakritiksiz)
- [ ] Commit hash'i adım raporunda belirtildi

## Aktif Modül Listesi

> Bu liste fazlar ilerledikçe güncellenir.

- (henüz modül eklenmedi)

## Referans Dokümanlar

- `WORKING_STYLE.md` — İletişim ve süreç kuralları
- `PROGRESS.md` — Mevcut durum
- `DEFERRED.md` — Ertelenen işler
- `docs/ARCHITECTURE.md` — (Faz 1'de oluşturulacak) Detaylı mimari
- `docs/MODULE_TEMPLATE.md` — (Faz 5'te oluşturulacak) Yeni modül kılavuzu
