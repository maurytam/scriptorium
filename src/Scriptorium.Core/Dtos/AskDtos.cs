using Scriptorium.Core.Ai;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Dtos;

public sealed record ChatExchangeDto(string Question, string Answer);

public sealed record AskRequestDto(string? Question, IReadOnlyList<ChatExchangeDto?>? History = null);

public sealed record AskResponseDto(string Answer, bool Truncated)
{
    /// <summary>Maps a successful result; a failed one has no answer and yields an empty one.</summary>
    public static AskResponseDto From(AskResult result) => new(result.Answer ?? string.Empty, result.Truncated);
}

/// <summary>What the web page needs to know before a question is sent.</summary>
public sealed record AskLimitsDto(int MaxQuestionLength)
{
    public static AskLimitsDto From(QaOptions options) => new(options.MaxQuestionLength);
}
