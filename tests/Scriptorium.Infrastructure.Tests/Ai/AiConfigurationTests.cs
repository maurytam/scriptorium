using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Interfaces;
using Scriptorium.Infrastructure.Ai;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Ai;

/// <summary>Checks the registration in Program.cs: the AI provider and the options bound from configuration.</summary>
public sealed class AiConfigurationTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void TheAiProvider_IsTheOllamaProvider()
    {
        _host.Factory.Services.GetRequiredService<IAIProvider>().Should().BeOfType<OllamaProvider>();
    }

    [Fact]
    public void TheDefaultsInAppSettings_AreBound()
    {
        var ollama = _host.Factory.Services.GetRequiredService<OllamaOptions>();
        var qa = _host.Factory.Services.GetRequiredService<QaOptions>();

        ollama.Should().Be(new OllamaOptions { BaseUrl = "http://localhost:11434", Model = "qwen3.5:4b", NumCtx = 8192, TimeoutSeconds = 120 });
        qa.Should().Be(new QaOptions { MaxQuestionLength = 2000, MaxDocumentCharacters = 8000, MaxHistoryExchanges = 10, MaxHistoryCharacters = 3000 });
    }

    [Fact]
    public void ConfiguredValues_OverrideTheDefaults()
    {
        using var factory = _host.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ollama:Model", "other-model");
            builder.UseSetting("Ollama:TimeoutSeconds", "7");
            builder.UseSetting("Qa:MaxDocumentCharacters", "123");
        });

        factory.Services.GetRequiredService<OllamaOptions>().Should().Match<OllamaOptions>(o => o.Model == "other-model" && o.TimeoutSeconds == 7);
        factory.Services.GetRequiredService<QaOptions>().MaxDocumentCharacters.Should().Be(123);
    }
}
