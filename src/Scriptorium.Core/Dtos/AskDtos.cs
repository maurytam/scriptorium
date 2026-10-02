using Scriptorium.Core.Results;

namespace Scriptorium.Core.Dtos;

public sealed record ChatExchangeDto(string Question, string Answer);

public sealed record AskRequestDto(string? Question, IReadOnlyList<ChatExchangeDto>? History = null);

public sealed record AskResponseDto(string Answer, bool Truncated)
{
    /// <summary>Maps a successful result; a failed one has no answer and yields an empty one.</summary>
    public static AskResponseDto From(AskResult result) => new(result.Answer ?? string.Empty, result.Truncated);
}
