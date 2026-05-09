using System.Globalization;
using Cms.Core.Data;
using Cms.Core.Modules;
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

var modules = builder.Services.AddCmsModules(builder.Configuration);

var app = builder.Build();

await modules.InstallCmsModulesAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapCmsModules(modules);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
