using System.Text.Json;
using FluentAssertions;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Dtos;

public class AskDtosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AskResponseDto_From_MapsAnswerAndTruncation(bool truncated)
    {
        var dto = AskResponseDto.From(AskResult.Success("Paris", truncated));

        dto.Should().Be(new AskResponseDto("Paris", truncated));
    }

    [Fact]
    public void AskResponseDto_From_AFailedResultYieldsAnEmptyAnswer()
    {
        var dto = AskResponseDto.From(AskResult.Failure(AskFailureKind.NotFound, "nope"));

        dto.Should().Be(new AskResponseDto(string.Empty, false));
    }

    [Fact]
    public void AskRequestDto_ReadsAQuestionWithoutHistory()
    {
        var dto = JsonSerializer.Deserialize<AskRequestDto>("""{"question":"Who signed?"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        dto.Should().Be(new AskRequestDto("Who signed?"));
        dto!.History.Should().BeNull();
    }

    [Fact]
    public void AskRequestDto_ReadsAQuestionWithHistory()
    {
        const string json = """{"question":"And when?","history":[{"question":"Who signed?","answer":"Maria"}]}""";

        var dto = JsonSerializer.Deserialize<AskRequestDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        dto!.Question.Should().Be("And when?");
        dto.History.Should().ContainSingle().Which.Should().Be(new ChatExchangeDto("Who signed?", "Maria"));
    }
}
