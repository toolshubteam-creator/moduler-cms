namespace Cms.Modules.Settings.Areas.Settings.Controllers;

using Cms.Core.Authorization;
using Cms.Modules.Settings.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Settings")]
[Authorize]
[HasPermission("settings.view")]
public sealed class SettingsController(ISettingsService settings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var entries = await settings.GetAllAsync(HttpContext.RequestAborted);
        return View(entries);
    }

    [HttpPost]
    [HasPermission("settings.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string key, string value, SettingValueType valueType)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            TempData["ErrorMessage"] = "Key bos olamaz.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            object? typed = valueType switch
            {
                SettingValueType.String => (object?)value,
                SettingValueType.Int => int.Parse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture),
                SettingValueType.Bool => bool.Parse(value),
                SettingValueType.Decimal => decimal.Parse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture),
                SettingValueType.Json => value,
                _ => value,
            };

            // Tip-spesifik SetAsync<T> cagrisi reflection ile yapilabilir; pratikte raw stringi
            // string olarak set ediyor + ValueType'i ekstra alanda saklamak istiyoruz. Servis
            // SetAsync<T> ValueType'i T'den infer ettigi icin burada object yerine concrete tip:
            switch (valueType)
            {
                case SettingValueType.String:
                    await settings.SetAsync(key, value, HttpContext.RequestAborted);
                    break;
                case SettingValueType.Int:
                    await settings.SetAsync(key, (int)typed!, HttpContext.RequestAborted);
                    break;
                case SettingValueType.Bool:
                    await settings.SetAsync(key, (bool)typed!, HttpContext.RequestAborted);
                    break;
                case SettingValueType.Decimal:
                    await settings.SetAsync(key, (decimal)typed!, HttpContext.RequestAborted);
                    break;
                case SettingValueType.Json:
                    // JSON raw string olarak saklanir; SetAsync<string> ValueType=String yazardi —
                    // burada raw deger zaten JSON metni, doğrudan SetAsync<object>(serialized) kullanma
                    // yerine internal helper'a guvenirsek SerializeValue gene JSON'a sarar. En basit:
                    // typed olarak raw string'i tutup SetAsync ile gondermek yerine raw'i string say.
                    // Bu adim Faz-4.2'de JSON-aware editor ile rafinelenecek.
                    await settings.SetAsync(key, value, HttpContext.RequestAborted);
                    break;
            }

            TempData["SuccessMessage"] = $"'{key}' kaydedildi.";
        }
        catch (FormatException ex)
        {
            TempData["ErrorMessage"] = $"Deger {valueType}'a uygun degil: {ex.Message}";
        }
        catch (OverflowException ex)
        {
            TempData["ErrorMessage"] = $"Deger {valueType} sinirini astı: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission("settings.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string key)
    {
        var ok = await settings.DeleteAsync(key, HttpContext.RequestAborted);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? $"'{key}' silindi." : $"'{key}' bulunamadi.";
        return RedirectToAction(nameof(Index));
    }
}
