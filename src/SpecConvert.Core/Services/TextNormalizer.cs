using System.Text.RegularExpressions;

namespace SpecConvert.Core.Services;

public static partial class TextNormalizer
{
    public static string Normalize(string? value) => Whitespace().Replace(Escapes().Replace(value ?? "", " "), " ").Trim();
    public static string Header(string? value) => NonLetters().Replace(Hyphenation().Replace(Normalize(value).ToLowerInvariant(), ""), "");
    public static string Join(IEnumerable<string> parts) => Normalize(string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s))));
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"_x000[ad9]_", RegexOptions.IgnoreCase)] private static partial Regex Escapes();
    [GeneratedRegex(@"-\s+")] private static partial Regex Hyphenation();
    [GeneratedRegex(@"[^\p{L}\p{N}]")] private static partial Regex NonLetters();
}
