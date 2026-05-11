namespace Cms.Modules.Settings.Domain;

using Cms.Core.Domain.Auditing;
using Cms.Modules.Settings.Contracts;

public sealed class SettingEntryEntity : IAuditable
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public SettingValueType ValueType { get; set; }

    public DateTime UpdatedAt { get; set; }
}
