using System.Text.RegularExpressions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// "Nothing is invented": keeps only the entities that can be found in the text the model was given, once each, written as the
/// text writes them and in the order they first appear. The comparison ignores letter case and counts any run of spaces or line
/// breaks as one space; it is a plain search, not a word search. A pure function.
/// </summary>
public static partial class EntityVerifier
{
    public static IReadOnlyList<EntityGroup> Verify(IReadOnlyList<EntityCandidate> candidates, string textRead)
    {
        var text = Collapse(textRead);
        var groups = new List<EntityGroup>();

        foreach (var type in Enum.GetValues<EntityType>())
        {
            var found = candidates
                .Where(c => c.Type == type)
                .Select(c => Find(text, Collapse(c.Text)))
                .OfType<Match>()
                .DistinctBy(m => m.Shown, StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m.Index)
                .Select(m => m.Shown)
                .ToList();

            if (found.Count > 0)
            {
                groups.Add(new EntityGroup(type, found));
            }
        }

        return groups;
    }

    // The text's own spelling of the first occurrence: both strings are collapsed, so the lengths are the same.
    private static Match? Find(string text, string entity)
    {
        if (entity.Length == 0)
        {
            return null;
        }

        var index = text.IndexOf(entity, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : new Match(index, text.Substring(index, entity.Length));
    }

    private static string Collapse(string value) => WhiteSpace().Replace(value, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    private sealed record Match(int Index, string Shown);
}
