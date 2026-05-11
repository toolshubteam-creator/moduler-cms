namespace Cms.Modules.Settings.Contracts;

public interface ISettingsService
{
    /// <summary>Tipli okuma. ValueType uyusmazsa <see cref="InvalidCastException"/>.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Raw string ve ValueType birlikte doner; deserialize karari caller'a kalir.</summary>
    Task<SettingEntry?> GetRawAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Mevcut key varsa update, yoksa insert. ValueType T'den infer edilir.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettingEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Hard delete (audit kalir, settings tablosundan satir gider).</summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
