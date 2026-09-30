using FluentAssertions;
using Moq;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentUploadServiceTests
{
    private const long MaxSizeBytes = 50L * 1024 * 1024;
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IFileStore> _fileStore = new();
    private readonly Mock<IDocumentRepository> _repository = new();
    private readonly Mock<IDocumentProcessingQueue> _queue = new();
    private readonly DocumentUploadService _service;

    public DocumentUploadServiceTests()
    {
        _fileStore.Setup(f => f.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("/data/doc/report.pdf"));
        _repository.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _repository.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(Now);
        _service = new DocumentUploadService(
            _fileStore.Object, _repository.Object, _queue.Object, Parsers("pdf", "docx", "xlsx", "txt"), clock.Object, MaxSizeBytes);
    }

    [Theory]
    [InlineData("report.pdf", "pdf")]
    [InlineData("Minutes.DOCX", "docx")]
    [InlineData("data.xlsx", "xlsx")]
    [InlineData("notes.txt", "txt")]
    public async Task UploadAsync_ValidFile_CreatesProcessingDocument(string fileName, string expectedType)
    {
        var result = await UploadAsync(fileName, 1234);

        result.IsSuccess.Should().BeTrue();
        result.Document.Should().BeEquivalentTo(new Document
        {
            Id = result.Document.Id,
            FileName = fileName,
            FileType = expectedType,
            FileSizeBytes = 1234,
            StoragePath = "/data/doc/report.pdf",
            UploadDate = Now,
            IsPrivate = false,
            Status = DocumentStatus.Processing
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_PersistsIsPrivateAsProvided(bool isPrivate)
    {
        var result = await _service.UploadAsync(new MemoryStream([1]), "notes.txt", 1, isPrivate, CancellationToken.None);

        result.Document!.IsPrivate.Should().Be(isPrivate);
        _repository.Verify(r => r.AddAsync(It.Is<Document>(d => d.IsPrivate == isPrivate), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_RejectedFile_DoesNotPersistIsPrivateDocument()
    {
        var result = await _service.UploadAsync(new MemoryStream([1]), "photo.png", 1, isPrivate: true, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _repository.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_FileExactlyAtLimit_IsAccepted()
    {
        var result = await UploadAsync("report.pdf", MaxSizeBytes);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_ValidFile_StoresPersistsAndEnqueuesSameDocument()
    {
        var result = await UploadAsync("report.pdf", 10);

        var id = result.Document!.Id;
        _fileStore.Verify(f => f.SaveAsync(id, "report.pdf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.AddAsync(It.Is<Document>(d => d.Id == id), It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.EnqueueAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedType_RejectsNamingSupportedFormats()
    {
        var result = await UploadAsync("photo.png", 10);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(UploadFailureKind.UnsupportedType);
        result.Error.Should().Be("Unsupported file type '.png'. Supported types: pdf, docx, xlsx, txt.");
        VerifyNothingStoredOrQueued();
    }

    [Fact]
    public async Task UploadAsync_FileWithoutExtension_IsRejectedAsUnsupported()
    {
        var result = await UploadAsync("README", 10);

        result.FailureKind.Should().Be(UploadFailureKind.UnsupportedType);
        result.Error.Should().Contain("(no extension)");
        VerifyNothingStoredOrQueued();
    }

    [Fact]
    public async Task UploadAsync_FileOverLimit_RejectsWithSizeMessage()
    {
        var result = await UploadAsync("report.pdf", MaxSizeBytes + 1);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(UploadFailureKind.FileTooLarge);
        result.Error.Should().Be("File exceeds the maximum size of 50 MB.");
        VerifyNothingStoredOrQueued();
    }

    [Fact]
    public async Task UploadAsync_StorageFails_ReturnsInternalFailureWithoutPersistingOrEnqueuing()
    {
        _fileStore.Setup(f => f.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("disk full"));

        var result = await UploadAsync("report.pdf", 10);

        result.FailureKind.Should().Be(UploadFailureKind.Internal);
        result.Error.Should().Be("disk full");
        _repository.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
        _queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_PersistFails_ReturnsInternalFailureWithoutEnqueuing()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("db down"));

        var result = await UploadAsync("report.pdf", 10);

        result.FailureKind.Should().Be(UploadFailureKind.Internal);
        _queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_EnqueueFails_MarksDocumentFailedAndReturnsInternalFailure()
    {
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("queue full"));

        var result = await UploadAsync("report.pdf", 10);

        result.FailureKind.Should().Be(UploadFailureKind.Internal);
        _repository.Verify(r => r.UpdateStatusAsync(
            It.IsAny<Guid>(), DocumentStatus.Failed, "queue full", It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyNothingStoredOrQueued()
    {
        _fileStore.Verify(f => f.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
        _queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IEnumerable<IDocumentParser> Parsers(params string[] fileTypes) =>
        fileTypes.Select(type =>
        {
            var parser = new Mock<IDocumentParser>();
            parser.Setup(p => p.SupportedFileType).Returns(type);
            return parser.Object;
        });

    private Task<UploadResult> UploadAsync(string fileName, long size) =>
        _service.UploadAsync(new MemoryStream([1, 2, 3]), fileName, size, isPrivate: false, CancellationToken.None);
}
