using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Scriptorium.API.ErrorHandling;

namespace Scriptorium.Infrastructure.Tests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _logger = new();
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests() => _handler = new GlobalExceptionHandler(_logger.Object);

    [Fact]
    public async Task TryHandleAsync_UnhandledException_Returns500WithGenericMessageAndLogsTheDetails()
    {
        var context = NewContext();
        var exception = new InvalidOperationException("secret internal detail");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = await ReadBodyAsync(context);
        body.Should().Be("An unexpected error occurred.");
        VerifyLogged(LogLevel.Error, exception);
    }

    [Fact]
    public async Task TryHandleAsync_ResponseNeverLeaksTheExceptionMessage()
    {
        var context = NewContext();

        await _handler.TryHandleAsync(context, new Exception("password=hunter2"), CancellationToken.None);

        context.Response.Body.Position = 0;
        (await new StreamReader(context.Response.Body).ReadToEndAsync()).Should().NotContain("hunter2");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    public async Task TryHandleAsync_BadHttpRequest_KeepsItsStatusAndIsNotLoggedAsAnError(int status)
    {
        var context = NewContext();
        var exception = new BadHttpRequestException("bad form value", status);

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(status);
        (await ReadBodyAsync(context)).Should().Be("The request was not valid.");
        _logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ClientDisconnected_WritesNothing()
    {
        using var cancellation = new CancellationTokenSource();
        var context = NewContext();
        context.RequestAborted = cancellation.Token;
        await cancellation.CancelAsync();

        var handled = await _handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.Body.Length.Should().Be(0);
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TryHandleAsync_CancellationNotCausedByTheClient_IsStillAnError()
    {
        var context = NewContext();

        await _handler.TryHandleAsync(context, new OperationCanceledException("timeout"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/documents";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string?> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        return json.RootElement.GetProperty("error").GetString();
    }

    private void VerifyLogged(LogLevel level, Exception exception) =>
        _logger.Verify(l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
}
