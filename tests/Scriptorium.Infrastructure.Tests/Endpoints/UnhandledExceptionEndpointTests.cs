using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Scriptorium.Core.Interfaces;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class UnhandledExceptionEndpointTests : IDisposable
{
    private readonly ApiTestHost _host = new();
    private readonly HttpClient _client;

    public UnhandledExceptionEndpointTests()
    {
        var failingRepository = new Mock<IDocumentRepository>();
        failingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database exploded: secret connection detail"));

        _client = _host.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentRepository>();
            services.AddSingleton(failingRepository.Object);
        })).CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task UnhandledException_ReturnsGenericJson500WithoutLeakingDetails()
    {
        var response = await _client.GetAsync($"/api/documents/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("An unexpected error occurred.");
        body.Should().NotContain("secret").And.NotContain("exploded").And.NotContain("at Scriptorium");
    }

    [Fact]
    public async Task AfterAnUnhandledException_TheApiKeepsServingOtherRequests()
    {
        (await _client.GetAsync($"/api/documents/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var limits = await _client.GetAsync("/api/documents/limits");

        limits.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidFormValue_StillAnswers400WithTheJsonErrorShape()
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(SampleDocuments.Txt("x")), "file", "a.txt" },
            { new StringContent("maybe"), "isPrivate" }
        };

        var response = await _client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("The request was not valid.");
    }
}
