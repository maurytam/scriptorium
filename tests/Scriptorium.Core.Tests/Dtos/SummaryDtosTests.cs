using System.Text.Json;
using FluentAssertions;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Dtos;

public class SummaryDtosTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"preference":null}""")]
    public void SummaryRequestDto_NoPreference_IsNull(string json)
    {
        var dto = JsonSerializer.Deserialize<SummaryRequestDto>(json, JsonSerializerOptions.Web);

        dto!.Preference.Should().BeNull();
    }

    [Fact]
    public void SummaryRequestDto_ReadsThePreference()
    {
        var dto = JsonSerializer.Deserialize<SummaryRequestDto>("""{"preference":"Cloud"}""", JsonSerializerOptions.Web);

        dto!.Preference.Should().Be("Cloud");
    }

    [Theory]
    [InlineData(false, ModelBackend.Local, "local")]
    [InlineData(true, ModelBackend.Cloud, "cloud")]
    public void SummaryResponseDto_From_MapsTextTruncationAndBackend(bool truncated, ModelBackend backend, string label)
    {
        var dto = SummaryResponseDto.From(AskResult.Success("A short paragraph.", truncated, backend));

        dto.Should().Be(new SummaryResponseDto("A short paragraph.", truncated, label));
    }

    [Fact]
    public void SummaryResponseDto_From_AFailedResultYieldsAnEmptySummary()
    {
        var dto = SummaryResponseDto.From(AskResult.Failure(AskFailureKind.NotFound, "nope"));

        dto.Should().Be(new SummaryResponseDto(string.Empty, false, "local"));
    }

    [Fact]
    public void SummaryResponseDto_TheJsonFieldsAreSummaryTruncatedAndBackend()
    {
        var json = JsonSerializer.Serialize(new SummaryResponseDto("text", true, "cloud"), JsonSerializerOptions.Web);

        json.Should().Be("""{"summary":"text","truncated":true,"backend":"cloud"}""");
    }
}
