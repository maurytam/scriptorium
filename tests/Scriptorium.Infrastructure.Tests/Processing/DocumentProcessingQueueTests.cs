using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Infrastructure.Processing;

namespace Scriptorium.Infrastructure.Tests.Processing;

public class DocumentProcessingQueueTests
{
    private readonly Mock<IDocumentRepository> _repository = new();
    private readonly Mock<IFileStore> _fileStore = new();
    private readonly Mock<IDocumentParser> _parser = new();
    private readonly TaskCompletionSource<(DocumentStatus Status, string? Reason)> _finalStatus = new();
    private readonly Document _document = new()
    {
        Id = Guid.NewGuid(), FileName = "a.txt", FileType = "txt", StoragePath = "/tmp/a.txt"
    };

    public DocumentProcessingQueueTests()
    {
        _parser.Setup(p => p.SupportedFileType).Returns("txt");
        _repository.Setup(r => r.GetByIdAsync(_document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_document);
        _repository.Setup(r => r.SaveExtractedTextAsync(It.IsAny<ExtractedText>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.UpdateStatusAsync(_document.Id, It.IsAny<DocumentStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DocumentStatus, string?, CancellationToken>((_, s, reason, _) => _finalStatus.TrySetResult((s, reason)))
            .ReturnsAsync(Result.Success());
        _fileStore.Setup(f => f.OpenRead(_document.StoragePath))
            .Returns(() => Result<Stream>.Success(new MemoryStream(Encoding.UTF8.GetBytes("content"))));
    }

    [Fact]
    public async Task Processing_ParserSucceeds_SavesTextAndMarksReady()
    {
        _parser.Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("extracted"));

        var status = await RunAsync();

        status.Status.Should().Be(DocumentStatus.Ready);
        _repository.Verify(r => r.SaveExtractedTextAsync(
            It.Is<ExtractedText>(t => t.DocumentId == _document.Id && t.Content == "extracted"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Processing_ParserFails_MarksFailedWithReason()
    {
        _parser.Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Corrupted or unreadable PDF"));

        var status = await RunAsync();

        status.Should().Be((DocumentStatus.Failed, "Corrupted or unreadable PDF"));
        _repository.Verify(r => r.SaveExtractedTextAsync(It.IsAny<ExtractedText>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Processing_ParserThrows_MarksFailedInsteadOfCrashing()
    {
        _parser.Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var status = await RunAsync();

        status.Status.Should().Be(DocumentStatus.Failed);
        status.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Processing_FileCannotBeOpened_MarksFailed()
    {
        _fileStore.Setup(f => f.OpenRead(_document.StoragePath)).Returns(Result<Stream>.Failure("Unable to read the stored file"));

        var status = await RunAsync();

        status.Should().Be((DocumentStatus.Failed, "Unable to read the stored file"));
    }

    [Fact]
    public async Task Processing_NoParserForFileType_MarksFailed()
    {
        _document.FileType = "png";

        var status = await RunAsync();

        status.Status.Should().Be(DocumentStatus.Failed);
        status.Reason.Should().Contain("png");
    }

    [Fact]
    public async Task EnqueueAsync_QueueFull_ReturnsFailure()
    {
        var queue = CreateQueue();
        for (var i = 0; i < 100; i++)
        {
            await queue.EnqueueAsync(Guid.NewGuid(), CancellationToken.None);
        }

        var overflow = await queue.EnqueueAsync(Guid.NewGuid(), CancellationToken.None);

        overflow.IsSuccess.Should().BeFalse();
    }

    private DocumentProcessingQueue CreateQueue() =>
        new(_repository.Object, _fileStore.Object, [_parser.Object], TimeProvider.System, NullLogger<DocumentProcessingQueue>.Instance);

    private async Task<(DocumentStatus Status, string? Reason)> RunAsync()
    {
        var queue = CreateQueue();
        await queue.StartAsync(CancellationToken.None);
        try
        {
            (await queue.EnqueueAsync(_document.Id, CancellationToken.None)).IsSuccess.Should().BeTrue();
            return await _finalStatus.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
