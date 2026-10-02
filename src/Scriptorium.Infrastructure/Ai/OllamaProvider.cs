using System.Net;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models.Exceptions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Ai;

/// <summary>The only class that knows about Ollama and its client library.</summary>
public sealed class OllamaProvider : IAIProvider
{
    private const float Temperature = 0.2f;
    private const string UnavailableMessage = "The answering model is not available. Check that Ollama is running and the model is installed.";
    private const string TimeoutMessage = "The model did not answer in time. Please try again.";
    private const string FailedMessage = "The model could not produce an answer.";

    private readonly IOllamaApiClient _client;
    private readonly OllamaOptions _options;

    public OllamaProvider(IOllamaApiClient client, OllamaOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <summary>Builds a provider that talks to the Ollama server named in the options.</summary>
    public static OllamaProvider Create(OllamaOptions options)
    {
        // The client library's own HttpClient gives up after 100 seconds; this provider enforces the configured timeout itself.
        var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl), Timeout = Timeout.InfiniteTimeSpan };
        return new OllamaProvider(new OllamaApiClient(http, options.Model), options);
    }

    public async Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var answer = await ReadAnswerAsync(ToChatRequest(request), timeout.Token);
            return answer.Length == 0
                ? AiResult.Failure(AiFailureKind.Failed, FailedMessage)
                : AiResult.Success(answer);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiResult.Failure(AiFailureKind.Timeout, TimeoutMessage);
        }
        catch (HttpRequestException ex)
        {
            return FromHttpFailure(ex);
        }
        catch (OllamaException)
        {
            return AiResult.Failure(AiFailureKind.Failed, FailedMessage);
        }
    }

    private async Task<string> ReadAnswerAsync(ChatRequest chatRequest, CancellationToken ct)
    {
        var answer = new StringBuilder();
        await foreach (var chunk in _client.ChatAsync(chatRequest, ct))
        {
            answer.Append(chunk?.Message?.Content);
        }

        return answer.ToString().Trim();
    }

    private ChatRequest ToChatRequest(AiRequest request) => new()
    {
        Model = _options.Model,
        Stream = true,
        Think = false, // thinking is on by default for this model and makes every answer many times slower
        Options = new RequestOptions { NumCtx = _options.NumCtx, Temperature = Temperature },
        Messages = request.Messages.Select(m => new Message(ToRole(m.Role), m.Content)).ToList()
    };

    private static ChatRole ToRole(AiRole role) => role switch
    {
        AiRole.System => ChatRole.System,
        AiRole.User => ChatRole.User,
        _ => ChatRole.Assistant
    };

    // No status code means the server could not be reached; 404 means the model is not installed.
    private static AiResult FromHttpFailure(HttpRequestException exception) =>
        exception.StatusCode is null or HttpStatusCode.NotFound
            ? AiResult.Failure(AiFailureKind.Unavailable, UnavailableMessage)
            : AiResult.Failure(AiFailureKind.Failed, FailedMessage);
}
