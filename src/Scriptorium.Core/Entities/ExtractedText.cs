namespace Scriptorium.Core.Entities;

public sealed class ExtractedText
{
    public Guid DocumentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset ExtractedAt { get; set; }
}
