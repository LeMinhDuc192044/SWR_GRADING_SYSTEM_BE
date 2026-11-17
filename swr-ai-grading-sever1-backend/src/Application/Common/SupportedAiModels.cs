namespace Application.Common;

/// <summary>
/// The Gemini models this program can use. AiModel validation and the grading fallback chain
/// both read this list, so they cannot drift apart.
/// </summary>
public static class SupportedAiModels
{
    /// <summary>Selectable models. The first three are also seeded into an empty database.</summary>
    public static readonly IReadOnlyList<string> Names =
    [
        "gemini-3-flash-preview",
        "gemini-3.8-flash",
        "gemini-3.5-flash-lite",
        "gemini-3.7-flash",
        "gemini-3.6-flash",
        "gemini-3.5-flash",
        "gemini-3.1-flash-lite"
    ];

    /// <summary>Rows created for a fresh database; the first one is enabled.</summary>
    public static readonly IReadOnlyList<string> Defaults = Names.Take(3).ToArray();

    private static readonly HashSet<string> Lookup = new(Names, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string name) => Lookup.Contains(name);

    public static string SupportedList => string.Join(", ", Names);
}
