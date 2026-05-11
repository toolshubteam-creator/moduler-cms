namespace Cms.Modules.Settings.Contracts;

public sealed record SettingEntry(string Key, string Value, SettingValueType ValueType, DateTime UpdatedAt);
