using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Dtos;

/// <summary>The optional body of a summary request. No body, or no preference, is Auto.</summary>
public sealed record SummaryRequestDto(string? Preference = null);

public sealed record SummaryResponseDto(string Summary, bool Truncated, string Backend = "local")
{
    /// <summary>Maps a successful result; a failed one has no text and yields an empty summary.</summary>
    public static SummaryResponseDto From(AskResult result) =>
        new(result.Answer ?? string.Empty, result.Truncated, result.Backend == ModelBackend.Cloud ? "cloud" : "local");
}
