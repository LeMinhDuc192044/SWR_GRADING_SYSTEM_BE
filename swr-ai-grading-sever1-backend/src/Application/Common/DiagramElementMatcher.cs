using System.Text.RegularExpressions;
using Domain.Entities;

namespace Application.Common;

/// <summary>
/// Connects an extracted answer to the diagram element it describes. Codes and names only match
/// as whole tokens: code "E1" must not match "E10", and a single-letter code such as "A" must not
/// match the article "a" in prose.
/// </summary>
public static class DiagramElementMatcher
{
    private const int MinCodeLength = 2;

    public static Guid? Match(IEnumerable<DiagramElement>? elements, params string?[] sources)
    {
        var candidates = elements?.ToList() ?? [];
        if (candidates.Count == 0)
            return null;

        var haystack = string.Join(
            "\n", sources.Where(source => !string.IsNullOrWhiteSpace(source)));

        if (!string.IsNullOrWhiteSpace(haystack))
        {
            var codeMatch = candidates
                .Select(element => new { Element = element, Code = element.ElementCode?.Trim() })
                .Where(candidate => candidate.Code is { Length: >= MinCodeLength })
                .Where(candidate => ContainsToken(haystack, candidate.Code!))
                .OrderByDescending(candidate => candidate.Code!.Length)
                .Select(candidate => candidate.Element)
                .FirstOrDefault();

            if (codeMatch is not null)
                return codeMatch.ElementId;

            var nameMatch = candidates
                .Select(element => new { Element = element, Name = element.Name?.Trim() })
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name))
                .Where(candidate => ContainsToken(haystack, candidate.Name!))
                .OrderByDescending(candidate => candidate.Name!.Length)
                .Select(candidate => candidate.Element)
                .FirstOrDefault();

            if (nameMatch is not null)
                return nameMatch.ElementId;
        }

        // A diagram with a single element is unambiguous even without textual evidence.
        return candidates.Count == 1 ? candidates[0].ElementId : null;
    }

    private static bool ContainsToken(string haystack, string token)
    {
        var pattern = $"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])";
        return Regex.IsMatch(haystack, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
