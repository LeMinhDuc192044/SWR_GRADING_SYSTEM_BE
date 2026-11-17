using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Extracts a student code from folder / archive names, ignoring any suffix such as "_1".
/// </summary>
public static class StudentCodeParser
{
    // Leading code only, and not followed by another digit (so SE1920441 is not read as SE192044).
    private static readonly Regex LeadingCode = new(
        @"^\s*([A-Za-z]{2}\d{5,8})(?![0-9])",
        RegexOptions.Compiled);

    private static readonly Regex AnyCode = new(
        @"[A-Za-z]{2}\d{5,8}",
        RegexOptions.Compiled);

    public static bool TryExtract(string? name, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var match = LeadingCode.Match(name);
        if (!match.Success)
            match = AnyCode.Match(name);

        if (!match.Success)
            return false;

        code = (match.Groups[1].Success ? match.Groups[1].Value : match.Value).ToUpperInvariant();
        return true;
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
