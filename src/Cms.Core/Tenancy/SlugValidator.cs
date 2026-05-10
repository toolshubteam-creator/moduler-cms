namespace Cms.Core.Tenancy;

using System.Text.RegularExpressions;

public static partial class SlugValidator
{
    [GeneratedRegex(@"^[a-z][a-z0-9-]{2,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static SlugValidationResult Validate(string? slug, IReadOnlyList<string> reservedSlugs)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return SlugValidationResult.Invalid("Slug bos olamaz.");
        }

        var normalized = slug.Trim().ToLowerInvariant();

        if (!SlugPattern().IsMatch(normalized))
        {
            return SlugValidationResult.Invalid("Slug formati gecersiz. Kucuk harf ile baslamali, 3-31 karakter, sadece a-z 0-9 ve tire.");
        }

        if (reservedSlugs.Any(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return SlugValidationResult.Invalid($"'{normalized}' rezerve edilmis bir slug, kullanilamaz.");
        }

        return SlugValidationResult.Valid(normalized);
    }
}

public sealed record SlugValidationResult(bool IsValid, string? Normalized, string? ErrorMessage)
{
    public static SlugValidationResult Valid(string normalized) => new(true, normalized, null);

    public static SlugValidationResult Invalid(string error) => new(false, null, error);
}
