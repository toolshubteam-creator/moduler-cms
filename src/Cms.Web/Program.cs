using System.Globalization;
using Cms.Core.Auth;
using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Cms.Core.Modules;
using Cms.Core.Tenancy;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
});

builder.Services.AddControllersWithViews();

builder.Services.AddCmsMaster(builder.Configuration);

builder.Services.AddCmsTenancy(builder.Configuration);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization();

var modules = builder.Services.AddCmsModules(builder.Configuration);

var app = builder.Build();

await modules.InstallCmsModulesAsync();

if (app.Environment.IsDevelopment())
{
    await SeedDevAdminAsync(app.Services, app.Configuration);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseTenantResolution();

app.UseAuthentication();
app.UseAuthorization();

app.MapCmsModules(modules);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task SeedDevAdminAsync(IServiceProvider sp, IConfiguration config)
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (await db.Users.AnyAsync())
    {
        return;
    }

    var email = config["Auth:DefaultAdmin:Email"] ?? "admin@cms.local";
    var password = config["Auth:DefaultAdmin:Password"] ?? "Cms_Admin_2026!Dev";

    var adminRole = new Role
    {
        Name = "Admin",
        Description = "System administrator",
        IsSystem = true,
        CreatedAtUtc = DateTime.UtcNow,
    };
    db.Roles.Add(adminRole);
    await db.SaveChangesAsync();

    var admin = new User
    {
        Email = email.Trim().ToLowerInvariant(),
        DisplayName = "Administrator",
        PasswordHash = hasher.Hash(password),
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
    };
    db.Users.Add(admin);
    await db.SaveChangesAsync();

    db.UserRoles.Add(new UserRole
    {
        UserId = admin.Id,
        RoleId = adminRole.Id,
        TenantId = null,
        AssignedAtUtc = DateTime.UtcNow,
    });
    await db.SaveChangesAsync();
}

public partial class Program;
