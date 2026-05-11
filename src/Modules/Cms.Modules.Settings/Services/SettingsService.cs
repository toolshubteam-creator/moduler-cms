namespace Cms.Modules.Settings.Services;

using System.Globalization;
using System.Text.Json;
using Cms.Core.Data;
using Cms.Modules.Settings.Contracts;
using Cms.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

public sealed class SettingsService(TenantDbContext db) : ISettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var row = await db.Set<SettingEntryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return default;
        }
        return (T?)ConvertValue(row.Value, row.ValueType, typeof(T));
    }

    public async Task<SettingEntry?> GetRawAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var row = await db.Set<SettingEntryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : new SettingEntry(row.Key, row.Value, row.ValueType, row.UpdatedAt);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var (raw, valueType) = SerializeValue(value);

        var existing = await db.Set<SettingEntryEntity>()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            db.Set<SettingEntryEntity>().Add(new SettingEntryEntity
            {
                Key = key,
                Value = raw,
                ValueType = valueType,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Value = raw;
            existing.ValueType = valueType;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SettingEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Set<SettingEntryEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new SettingEntry(r.Key, r.Value, r.ValueType, r.UpdatedAt))];
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var existing = await db.Set<SettingEntryEntity>()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }
        db.Set<SettingEntryEntity>().Remove(existing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static (string Raw, SettingValueType ValueType) SerializeValue<T>(T value)
    {
        return value switch
        {
            null => (string.Empty, SettingValueType.String),
            string s => (s, SettingValueType.String),
            int i => (i.ToString(CultureInfo.InvariantCulture), SettingValueType.Int),
            long l => (l.ToString(CultureInfo.InvariantCulture), SettingValueType.Int),
            bool b => (b ? "true" : "false", SettingValueType.Bool),
            decimal d => (d.ToString(CultureInfo.InvariantCulture), SettingValueType.Decimal),
            double f => (f.ToString(CultureInfo.InvariantCulture), SettingValueType.Decimal),
            _ => (JsonSerializer.Serialize(value, _jsonOptions), SettingValueType.Json),
        };
    }

    private static object? ConvertValue(string raw, SettingValueType valueType, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            return valueType switch
            {
                SettingValueType.String => underlying == typeof(string)
                    ? raw
                    : throw new InvalidCastException($"ValueType=String '{targetType.Name}' tipine cevrilemez."),

                SettingValueType.Int => underlying == typeof(int)
                    ? int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture)
                    : underlying == typeof(long)
                        ? (object)long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture)
                        : throw new InvalidCastException($"ValueType=Int '{targetType.Name}' tipine cevrilemez."),

                SettingValueType.Bool => underlying == typeof(bool)
                    ? bool.Parse(raw)
                    : throw new InvalidCastException($"ValueType=Bool '{targetType.Name}' tipine cevrilemez."),

                SettingValueType.Decimal => underlying == typeof(decimal)
                    ? decimal.Parse(raw, NumberStyles.Number, CultureInfo.InvariantCulture)
                    : underlying == typeof(double)
                        ? (object)double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture)
                        : throw new InvalidCastException($"ValueType=Decimal '{targetType.Name}' tipine cevrilemez."),

                SettingValueType.Json => JsonSerializer.Deserialize(raw, underlying, _jsonOptions),

                _ => throw new InvalidOperationException($"Bilinmeyen ValueType: {valueType}"),
            };
        }
        catch (FormatException ex)
        {
            throw new InvalidCastException($"'{raw}' degeri ValueType={valueType}'a uygun degil.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidCastException($"JSON parse hatasi: {ex.Message}", ex);
        }
    }
}
