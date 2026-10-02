namespace Scriptorium.Infrastructure.Ai;

/// <summary>Where Ollama listens and how it is asked to answer (bound from the "Ollama" configuration section).</summary>
public sealed record OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "qwen3.5:4b";

    /// <summary>Working window requested from Ollama, in tokens. Ollama cuts a longer prompt silently, so it is always set.</summary>
    public int NumCtx { get; init; } = 8192;

    public int TimeoutSeconds { get; init; } = 120;
}
