namespace Scriptorium.Infrastructure.Ai;

/// <summary>How the cloud model is asked to answer (bound from the "Anthropic" configuration section).</summary>
public sealed record AnthropicOptions
{
    /// <summary>
    /// The key of the cloud service. It comes only from <c>dotnet user-secrets</c> or an environment variable
    /// (<c>Anthropic__ApiKey</c>), never from a file that is committed. Without it the cloud model cannot be used.
    /// </summary>
    public string? ApiKey { get; init; }

    public string Model { get; init; } = "claude-sonnet-4-20250514";

    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>The longest answer the cloud model may produce, in tokens.</summary>
    public int MaxTokens { get; init; } = 1024;
}
