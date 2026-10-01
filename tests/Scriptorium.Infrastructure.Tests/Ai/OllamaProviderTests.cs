using System.Net;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Moq;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models.Exceptions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Infrastructure.Ai;

namespace Scriptorium.Infrastructure.Tests.Ai;

public class OllamaProviderTests
{
    private static readonly OllamaOptions Options = new() { Model = "test-model", NumCtx = 4096, TimeoutSeconds = 30 };

    private readonly Mock<IOllamaApiClient> _client = new();

    [Fact]
    public async Task CompleteAsync_AssemblesTheStreamedChunksIntoOneTrimmedAnswer()
    {
        SetupChunks("  The capital ", "of France ", "is Paris.\n");

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("The capital of France is Paris.");
    }

    [Fact]
    public async Task CompleteAsync_SendsTheConfiguredModelWindowTemperatureAndNoThinking()
    {
        var sent = CaptureRequest();

        await Provider().CompleteAsync(Request(), CancellationToken.None);

        sent.Value!.Model.Should().Be("test-model");
        sent.Value.Options!.NumCtx.Should().Be(4096);
        sent.Value.Options.Temperature.Should().Be(0.2f);
        sent.Value.Think.Should().Be((ThinkValue)false);
        sent.Value.Stream.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_SendsTheMessagesInOrderWithTheirRoles()
    {
        var sent = CaptureRequest();
        var request = new AiRequest(
        [
            new AiMessage(AiRole.System, "rules"),
            new AiMessage(AiRole.User, "first question"),
            new AiMessage(AiRole.Assistant, "first answer"),
            new AiMessage(AiRole.User, "second question")
        ]);

        await Provider().CompleteAsync(request, CancellationToken.None);

        sent.Value!.Messages!.Select(m => (m.Role, m.Content)).Should().Equal(
            (ChatRole.System, "rules"),
            (ChatRole.User, "first question"),
            (ChatRole.Assistant, "first answer"),
            (ChatRole.User, "second question"));
    }

    [Fact]
    public async Task CompleteAsync_EmptyAnswer_ReturnsFailed()
    {
        SetupChunks("   ");

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Failed);
    }

    [Fact]
    public async Task CompleteAsync_ServerNotReachable_ReturnsUnavailable()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Throwing(new HttpRequestException("Connection refused")));

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
        result.Error.Should().Contain("Ollama is running");
    }

    [Fact]
    public async Task CompleteAsync_ModelNotInstalled_ReturnsUnavailable()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Throwing(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound)));

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
    }

    [Fact]
    public async Task CompleteAsync_ServerError_ReturnsFailedWithoutLeakingDetails()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Throwing(new HttpRequestException("secret internal detail", null, HttpStatusCode.InternalServerError)));

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Failed);
        result.Error.Should().NotContain("secret");
    }

    [Fact]
    public async Task CompleteAsync_LibraryException_ReturnsFailed()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Throwing(new OllamaException("boom")));

        var result = await Provider().CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Failed);
    }

    [Fact]
    public async Task CompleteAsync_NoAnswerWithinTheConfiguredTime_ReturnsTimeout()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ChatRequest _, CancellationToken token) => Hang(token));
        var provider = new OllamaProvider(_client.Object, Options with { TimeoutSeconds = 1 });

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Timeout);
    }

    [Fact]
    public async Task CompleteAsync_CancelledByTheCaller_PropagatesInsteadOfReportingATimeout()
    {
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ChatRequest _, CancellationToken token) => Hang(token));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => Provider().CompleteAsync(Request(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Create_BuildsAProviderThatReportsUnavailableWhenNothingListens()
    {
        var provider = OllamaProvider.Create(Options with { BaseUrl = "http://localhost:9" });

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
    }

    private OllamaProvider Provider() => new(_client.Object, Options);

    private static AiRequest Request() => new([new AiMessage(AiRole.User, "hello")]);

    private void SetupChunks(params string[] parts) =>
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>())).Returns(Chunks(parts));

    private StrongBox<ChatRequest?> CaptureRequest()
    {
        var box = new StrongBox<ChatRequest?>();
        _client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((request, _) => box.Value = request)
            .Returns(Chunks("ok"));
        return box;
    }

    private static async IAsyncEnumerable<ChatResponseStream?> Chunks(params string[] parts)
    {
        foreach (var part in parts)
        {
            await Task.Yield();
            yield return new ChatResponseStream { Message = new Message(ChatRole.Assistant, part) };
        }
    }

    private static async IAsyncEnumerable<ChatResponseStream?> Throwing(Exception exception)
    {
        await Task.Yield();
        throw exception;
#pragma warning disable CS0162 // required to make this an iterator
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<ChatResponseStream?> Hang([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        yield break;
    }
}
