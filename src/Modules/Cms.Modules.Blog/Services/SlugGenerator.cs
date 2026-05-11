namespace Cms.Modules.Blog.Services;

using System.Text;
using System.Text.RegularExpressions;

internal static partial class SlugGenerator
{
    private const int MaxLength = 200;

    private static readonly Dictionary<char, string> _turkishMap = new()
    {
        ['ı'] = "i",
        ['İ'] = "i",
        ['ş'] = "s",
        ['Ş'] = "s",
        ['ç'] = "c",
        ['Ç'] = "c",
        ['ğ'] = "g",
        ['Ğ'] = "g",
        ['ü'] = "u",
        ['Ü'] = "u",
        ['ö'] = "o",
        ['Ö'] = "o",
    };

    [GeneratedRegex("-+")]
    private static partial Regex MultiDash();

    public static string Generate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            if (_turkishMap.TryGetValue(ch, out var replacement))
            {
                sb.Append(replacement);
            }
            else if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                sb.Append('-');
            }
        }

        var slug = MultiDash().Replace(sb.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength] : slug;
    }
}
