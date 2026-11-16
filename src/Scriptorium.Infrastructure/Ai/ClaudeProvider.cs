using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Ai;

/// <summary>The only class that knows about the cloud model and its client library.</summary>
public sealed class ClaudeProvider : ICloudAIProvider
{
    private const string UnavailableMessage = "The cloud model is not available. Check the cloud settings and your connection.";
    private const string TimeoutMessage = "The cloud model did not answer in time. Please try again.";
    private const string FailedMessage = "The cloud model could not produce an answer.";

    private readonly AnthropicOptions _options;
    private readonly HttpClient _http;

    // Created on the first request and only when there is a key: the library would otherwise look for a key in the
    // environment of the machine, and building the provider must never fail or send anything.
    private readonly Lazy<AnthropicClient> _client;

    public ClaudeProvider(AnthropicOptions options, HttpClient http)
    {
        _options = options;
        _http = http;
        _client = new Lazy<AnthropicClient>(CreateClient);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>Builds a provider that will talk to the cloud service named by the library, when it has a key.</summary>
    public static ClaudeProvider Create(AnthropicOptions options)
    {
        // The configured timeout is enforced by the provider itself; the HttpClient's own 100 seconds must not cut in first.
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        return new ClaudeProvider(options, http);
    }

    public async Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return AiResult.Failure(AiFailureKind.Unavailable, UnavailableMessage);
        }

        try
        {
            return await AskAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiResult.Failure(AiFailureKind.Timeout, TimeoutMessage);
        }
        catch (Exception ex) when (ex is AnthropicIOException or HttpRequestException)
        {
            return AiResult.Failure(AiFailureKind.Unavailable, UnavailableMessage);
        }
        catch (AnthropicApiException ex)
        {
            return FromApiFailure(ex);
        }
        catch (AnthropicException)
        {
            return AiResult.Failure(AiFailureKind.Failed, FailedMessage);
        }
    }

    private async Task<AiResult> AskAsync(AiRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var message = await _client.Value.Messages.Create(ToParams(request), timeout.Token);
        var answer = ReadText(message);
        return answer.Length == 0
            ? AiResult.Failure(AiFailureKind.Failed, FailedMessage)
            : AiResult.Success(answer);
    }

    private AnthropicClient CreateClient() => new()
    {
        ApiKey = _options.ApiKey, // always given explicitly, so a key in the environment of the machine is never used
        HttpClient = _http,
        Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        MaxRetries = 0 // one question is one request
    };

    private MessageCreateParams ToParams(AiRequest request) => new()
    {
        Model = _options.Model,
        MaxTokens = _options.MaxTokens,
        System = SystemText(request),
        Messages = request.Messages.Where(m => m.Role != AiRole.System).Select(ToMessage).ToList()
    };

    private static string SystemText(AiRequest request) =>
        string.Join("\n\n", request.Messages.Where(m => m.Role == AiRole.System).Select(m => m.Content));

    private static MessageParam ToMessage(AiMessage message) => message.Role == AiRole.Assistant
        ? new AssistantMessageParam { Content = message.Content }
        : new UserMessageParam { Content = message.Content };

    private static string ReadText(Message message)
    {
        var texts = message.Content.Select(block => block.TryPickText(out var text) ? text.Text : string.Empty);
        return string.Concat(texts).Trim();
    }

    // A refused key is something the owner can fix in the settings; anything else stays generic.
    private static AiResult FromApiFailure(AnthropicApiException exception) =>
        exception.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
            ? AiResult.Failure(AiFailureKind.Unavailable, UnavailableMessage)
            : AiResult.Failure(AiFailureKind.Failed, FailedMessage);
}
