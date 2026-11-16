using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Infrastructure.Ai;

namespace Scriptorium.Infrastructure.Tests.Ai;

/// <summary>
/// The cloud provider is exercised with a fake HTTP handler: the real library code runs against canned replies and
/// nothing ever reaches the network. No test here uses a real key.
/// </summary>
public class ClaudeProviderTests
{
    private const string FakeKey = "test-key-not-real";

    private static readonly AnthropicOptions Options = new() { ApiKey = FakeKey, Model = "test-model", MaxTokens = 321, TimeoutSeconds = 30 };

    private readonly StubHandler _handler = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CompleteAsync_WithoutAKey_IsNotConfiguredAndFailsAsUnavailableWithoutAnyRequest(string? key)
    {
        var provider = Provider(Options with { ApiKey = key });

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        provider.IsConfigured.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
        result.Error.Should().Be("The cloud model is not available. Check the cloud settings and your connection.");
        _handler.Calls.Should().Be(0);
    }

    [Fact]
    public void Constructing_WithoutAKey_DoesNotThrowAndSendsNothing()
    {
        var build = () => Provider(Options with { ApiKey = null });

        build.Should().NotThrow();
        _handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CompleteAsync_AKeyInTheEnvironmentOfTheLibrary_IsIgnored()
    {
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "key-from-the-shell-must-not-count");
        try
        {
            var result = await Provider(Options with { ApiKey = null }).CompleteAsync(Request(), CancellationToken.None);

            result.FailureKind.Should().Be(AiFailureKind.Unavailable);
            _handler.Calls.Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }
    }

    [Fact]
    public void IsConfigured_WithAKey_IsTrue()
    {
        Provider(Options).IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_ReturnsTheTrimmedAnswerText()
    {
        _handler.Reply(HttpStatusCode.OK, MessageJson("  The capital is Paris.\n"));

        var result = await Provider(Options).CompleteAsync(Request(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("The capital is Paris.");
    }

    [Fact]
    public async Task CompleteAsync_SendsTheOptionsTheSystemTextAndTheMessagesAndTheConfiguredKey()
    {
        _handler.Reply(HttpStatusCode.OK, MessageJson("ok"));
        var request = new AiRequest([
            new AiMessage(AiRole.System, "Answer only from the document."),
            new AiMessage(AiRole.User, "What is the capital?"),
            new AiMessage(AiRole.Assistant, "Paris."),
            new AiMessage(AiRole.User, "And the country?")
        ]);

        await Provider(Options).CompleteAsync(request, CancellationToken.None);

        _handler.Calls.Should().Be(1);
        _handler.Headers.Should().Contain(FakeKey);
        using var body = JsonDocument.Parse(_handler.Body);
        body.RootElement.GetProperty("model").GetString().Should().Be("test-model");
        body.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(321);
        body.RootElement.GetProperty("system").ToString().Should().Contain("Answer only from the document.");
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToList();
        messages.Select(m => m.GetProperty("role").GetString()).Should().Equal("user", "assistant", "user");
    }

    [Fact]
    public async Task CompleteAsync_AnEmptyReply_IsFailed()
    {
        _handler.Reply(HttpStatusCode.OK, MessageJson("   "));

        var result = await Provider(Options).CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Failed);
        result.Error.Should().Be("The cloud model could not produce an answer.");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CompleteAsync_ARefusedKey_IsUnavailableAndPointsAtTheSettings(HttpStatusCode status)
    {
        _handler.Reply(status, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid\"}}");

        var result = await Provider(Options).CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
        result.Error.Should().Be("The cloud model is not available. Check the cloud settings and your connection.");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CompleteAsync_AnyOtherErrorStatus_IsFailedAndIsNotRetried(HttpStatusCode status)
    {
        _handler.Reply(status, "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"boom\"}}");

        var result = await Provider(Options).CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Failed);
        result.Error.Should().Be("The cloud model could not produce an answer.");
        _handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CompleteAsync_ServiceNotReachable_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("no route to host"));

        var result = await Provider(Options).CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Unavailable);
        result.Error.Should().Be("The cloud model is not available. Check the cloud settings and your connection.");
    }

    [Fact]
    public async Task CompleteAsync_NoAnswerWithinTheConfiguredTime_IsATimeout()
    {
        _handler.NeverAnswer();

        var result = await Provider(Options with { TimeoutSeconds = 1 }).CompleteAsync(Request(), CancellationToken.None);

        result.FailureKind.Should().Be(AiFailureKind.Timeout);
        result.Error.Should().Be("The cloud model did not answer in time. Please try again.");
    }

    [Fact]
    public async Task CompleteAsync_CancelledByTheCaller_PropagatesInsteadOfReportingATimeout()
    {
        _handler.NeverAnswer();
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        Func<Task> act = () => Provider(Options).CompleteAsync(Request(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Messages_SayCloudModelAndNeverMentionOllama()
    {
        var unavailable = await Provider(Options with { ApiKey = null }).CompleteAsync(Request(), CancellationToken.None);

        unavailable.Error.Should().Contain("cloud model").And.NotContain("Ollama");
    }

    private ClaudeProvider Provider(AnthropicOptions options) => new(options, new HttpClient(_handler));

    private static AiRequest Request() => new([new AiMessage(AiRole.User, "What is the capital of France?")]);

    private static string MessageJson(string text) => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        model = "test-model",
        content = new[] { new { type = "text", text } },
        stop_reason = "end_turn",
        stop_sequence = (string?)null,
        usage = new { input_tokens = 1, output_tokens = 1 }
    });

    /// <summary>Answers every request from memory, remembers what it was asked, and never touches the network.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<CancellationToken, Task<HttpResponseMessage>> _respond =
            _ => throw new InvalidOperationException("No reply was set up for this test.");

        public int Calls { get; private set; }

        public string Headers { get; private set; } = string.Empty;

        public string Body { get; private set; } = string.Empty;

        public void Reply(HttpStatusCode status, string json) =>
            _respond = _ => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        public void Fail(Exception exception) => _respond = _ => throw exception;

        public void NeverAnswer() =>
            _respond = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Headers = request.Headers.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respond(cancellationToken);
        }
    }
}
