using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Scriptorium.API.Endpoints;
using Scriptorium.Core.Enums;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public class AskFailureResponsesTests
{
    [Theory]
    [InlineData(AskFailureKind.InvalidQuestion, 400)]
    [InlineData(AskFailureKind.NotFound, 404)]
    [InlineData(AskFailureKind.NotReady, 409)]
    [InlineData(AskFailureKind.PreferenceNotAllowed, 409)]
    [InlineData(AskFailureKind.NotEnoughText, 409)]
    [InlineData(AskFailureKind.ModelUnavailable, 503)]
    [InlineData(AskFailureKind.Timeout, 504)]
    [InlineData(AskFailureKind.Internal, 500)]
    public async Task From_MapsEveryKindToItsStatus(AskFailureKind kind, int status)
    {
        var (code, _) = await ExecuteAsync(AskFailureResponses.From(kind, "message"));

        code.Should().Be(status);
    }

    [Fact]
    public async Task From_NoKind_Is500()
    {
        var (code, _) = await ExecuteAsync(AskFailureResponses.From(null, "message"));

        code.Should().Be(500);
    }

    [Fact]
    public async Task From_TheBodyIsTheErrorMessage()
    {
        var (_, body) = await ExecuteAsync(AskFailureResponses.From(AskFailureKind.NotFound, "Document not found."));

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().Should().Be("Document not found.");
    }

    private static async Task<(int Status, string Body)> ExecuteAsync(IResult result)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }
}
