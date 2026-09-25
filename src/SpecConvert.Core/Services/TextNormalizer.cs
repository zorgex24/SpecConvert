using System.Text.RegularExpressions;

namespace SpecConvert.Core.Services;

public static partial class TextNormalizer
{
    public static string Normalize(string? value) => Whitespace().Replace(Escapes().Replace(value ?? "", " "), " ").Trim();
    // Apply only after logical blocks have been assembled: source numbering still
    // identifies block boundaries, and dimensions inside the name must survive.
    public static string Name(string? value) => LeadingNonLetters().Replace(Normalize(value), "");
    public static string Header(string? value) => NonLetters().Replace(Hyphenation().Replace(Normalize(value).ToLowerInvariant(), ""), "");
    public static string Join(IEnumerable<string> parts) => Normalize(string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s))));
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"^[^\p{L}]+")] private static partial Regex LeadingNonLetters();
    [GeneratedRegex(@"_x000[ad9]_", RegexOptions.IgnoreCase)] private static partial Regex Escapes();
    [GeneratedRegex(@"-\s+")] private static partial Regex Hyphenation();
    [GeneratedRegex(@"[^\p{L}\p{N}]")] private static partial Regex NonLetters();
}
