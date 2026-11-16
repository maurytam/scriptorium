namespace Scriptorium.Core.Services;

/// <summary>
/// A text cut at a limit of characters. The question flow and the text preview both use it, so what the model reads
/// and what the user sees stop at the same character.
/// </summary>
/// <param name="Text">The text, or its first characters when it was cut.</param>
/// <param name="Truncated">True only when the original was longer than the limit.</param>
/// <param name="OriginalLength">The length of the original text, in characters.</param>
public sealed record TextExcerpt(string Text, bool Truncated, int OriginalLength)
{
    public static TextExcerpt Of(string text, int maxCharacters)
    {
        if (text.Length <= maxCharacters)
        {
            return new TextExcerpt(text, false, text.Length);
        }

        var cut = maxCharacters;
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
        {
            cut--; // never end on half of a character
        }

        return new TextExcerpt(text[..cut], true, text.Length);
    }
}
