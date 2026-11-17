using System.Text;

namespace Scriptorium.Infrastructure.Parsing;

/// <summary>Cleaning of a signer's name, whatever file it was read from.</summary>
public static class SignerNameText
{
    private const int MaxLength = 100;

    /// <summary>
    /// The name comes from the file and ends up in the text the model reads: keep it one short line of plain text
    /// (no control characters, white space collapsed, at most 100 characters, never half of a surrogate pair).
    /// </summary>
    public static string? Clean(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var text = new StringBuilder();
        var pendingSpace = false;
        foreach (var character in name)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                pendingSpace = text.Length > 0;
                continue;
            }

            AppendWithPendingSpace(text, character, ref pendingSpace);
        }

        return Cut(text.ToString());
    }

    private static void AppendWithPendingSpace(StringBuilder text, char character, ref bool pendingSpace)
    {
        if (pendingSpace)
        {
            text.Append(' ');
            pendingSpace = false;
        }

        text.Append(character);
    }

    private static string? Cut(string text)
    {
        var cut = text.Length > MaxLength ? text[..MaxLength].TrimEnd() : text;
        if (cut.Length > 0 && char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return cut.Length == 0 ? null : cut;
    }
}
