using System.Text.RegularExpressions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Services;

/// <summary>
/// Reads the model's reply, one entity per line as <c>kind: text</c>. A line that cannot be read is ignored, so a model that adds a
/// sentence or gets one line wrong loses only that. A pure function: it never throws on a strange reply.
/// </summary>
public static partial class EntityReplyParser
{
    private static readonly Dictionary<string, EntityType> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = EntityType.Person,
        ["persons"] = EntityType.Person,
        ["people"] = EntityType.Person,
        ["organization"] = EntityType.Organization,
        ["organizations"] = EntityType.Organization,
        ["organisation"] = EntityType.Organization,
        ["organisations"] = EntityType.Organization,
        ["date"] = EntityType.Date,
        ["dates"] = EntityType.Date,
        ["amount"] = EntityType.Amount,
        ["amounts"] = EntityType.Amount,
        ["place"] = EntityType.Place,
        ["places"] = EntityType.Place
    };

    public static EntityReply Parse(string reply)
    {
        var candidates = reply.Split('\n').Select(ParseLine).OfType<EntityCandidate>().ToList();
        if (candidates.Count > 0)
        {
            return new EntityReply(EntityReplyKind.Entities, candidates);
        }

        return new EntityReply(IsNone(reply) ? EntityReplyKind.None : EntityReplyKind.Unreadable, []);
    }

    // A bullet or a number in front of the line is dropped, then the line is split at the first colon only.
    private static EntityCandidate? ParseLine(string line)
    {
        var withoutBullet = LeadingBullet().Replace(line.Trim(), string.Empty);
        var colon = withoutBullet.IndexOf(':');
        if (colon <= 0)
        {
            return null;
        }

        var text = withoutBullet[(colon + 1)..].Trim();
        var known = Kinds.TryGetValue(withoutBullet[..colon].Trim(), out var type);
        return known && text.Length > 0 ? new EntityCandidate(type, text) : null;
    }

    private static bool IsNone(string reply) =>
        string.Equals(reply.Trim().TrimEnd('.'), "NONE", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^(?:[-*•]|\d+[.)])\s*")]
    private static partial Regex LeadingBullet();
}
