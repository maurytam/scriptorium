namespace Scriptorium.Core.Services;

/// <summary>Fences a document off from the instructions of a prompt, so the text is read as data.</summary>
public static class PromptDocumentBlock
{
    private const string ClosingTag = "</document>";

    public static string Wrap(string text) => $"<document>\n{Neutralize(text)}\n{ClosingTag}";

    // A document must not be able to close the tag that fences it off from the instructions.
    private static string Neutralize(string text) =>
        text.Replace(ClosingTag, "</ document>", StringComparison.OrdinalIgnoreCase);
}
