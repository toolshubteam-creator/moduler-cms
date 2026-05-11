namespace Cms.Modules.Media;

public sealed class MediaStorageOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Disk root. Relative path verilirse ContentRootPath bazli — production'da
    /// absolute path veya konfigurasyon zorunlu.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/media";
}
