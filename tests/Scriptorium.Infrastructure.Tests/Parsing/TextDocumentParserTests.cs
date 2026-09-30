using System.Text;
using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class TextDocumentParserTests
{
    private readonly TextDocumentParser _parser = new();

    [Fact]
    public void SupportedFileType_IsTxt()
    {
        _parser.SupportedFileType.Should().Be("txt");
    }

    [Fact]
    public async Task ExtractTextAsync_Utf8Content_ReturnsContent()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Caffè — naïve text"));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Caffè — naïve text");
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyFile_ReturnsEmptyContent()
    {
        using var stream = new MemoryStream();

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractTextAsync_UnreadableStream_ReturnsFailure()
    {
        using var stream = new ThrowingStream();

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unable to read");
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("boom");
    }
}
