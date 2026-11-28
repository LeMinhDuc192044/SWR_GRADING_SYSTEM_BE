using System.Text;
using Domain.Entities;

namespace Application.Common;

/// <summary>
/// Assigns a question to a diagram when the question's title or content mentions the diagram's name
/// ("Use Case Diagram" -> diagram "Use case"). Matching is done on normalized whole words, so a
/// fragment of a longer word never produces an assignment.
/// </summary>
public static class DiagramNameMatcher
{
    // Words that say "this is a diagram" but not which one, so they are dropped from a name.
    private static readonly string[] GenericWords =
        ["diagram", "diagrams", "chart", "model", "sơ", "đồ", "biểu", "hình"];

    private static readonly string[] Notations =
    [
        "use case", "usecase", "class", "activity", "sequence", "state", "erd", "entity relationship",
        "context", "swimlane", "component", "deployment", "communication", "object", "package", "dfd",
        "data flow", "flowchart", "gantt", "network", "architecture"
    ];

    /// <summary>
    /// The diagram whose name is mentioned in the title/content, or null when none is mentioned.
    /// The longest mention wins, so "Use Case Diagram" beats a diagram merely called "Case".
    /// </summary>
    public static Guid? Match(string? title, string? content, IReadOnlyList<Diagram>? diagrams)
    {
        if (diagrams is null || diagrams.Count == 0)
            return null;

        var haystack = Normalize($"{title}\n{content}");
        if (string.IsNullOrWhiteSpace(haystack))
            return null;
        haystack = $" {haystack} ";

        Guid? bestId = null;
        var bestScore = 0;

        foreach (var diagram in diagrams)
        {
            foreach (var keyword in BuildKeywords(diagram.Name))
            {
                if (keyword.Length <= bestScore || !haystack.Contains($" {keyword} ", StringComparison.Ordinal))
                    continue;

                bestScore = keyword.Length;
                bestId = diagram.DiagramId;
            }
        }

        return bestId;
    }

    private static IEnumerable<string> BuildKeywords(string? name)
    {
        var full = Normalize(name);
        if (string.IsNullOrWhiteSpace(full))
            yield break;

        yield return full;

        var stripped = string.Join(' ', full.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !GenericWords.Contains(word, StringComparer.Ordinal)));

        if (stripped.Length >= 3 && !string.Equals(stripped, full, StringComparison.Ordinal))
            yield return stripped;

        // A diagram named after the notation itself ("Use Case Diagram", "ERD") is also keyed by
        // that notation when the question mentions it in another phrase.
        foreach (var notation in Notations)
            if (full.Contains(notation, StringComparison.Ordinal))
                yield return notation;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
