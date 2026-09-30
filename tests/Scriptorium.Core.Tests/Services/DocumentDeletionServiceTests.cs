using FluentAssertions;
using Moq;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentDeletionServiceTests
{
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IFileStore> _fileStore = new(MockBehavior.Strict);
    private readonly DocumentDeletionService _service;
    private readonly Guid _id = Guid.NewGuid();

    public DocumentDeletionServiceTests() => _service = new DocumentDeletionService(_repository.Object, _fileStore.Object);

    [Theory]
    [InlineData(DocumentStatus.Ready)]
    [InlineData(DocumentStatus.Failed)]
    public async Task DeleteAsync_FinishedDocument_RemovesRowThenFiles(DocumentStatus status)
    {
        var order = new List<string>();
        Existing(status);
        _repository.Setup(r => r.DeleteAsync(_id, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("database")).ReturnsAsync(Result.Success());
        _fileStore.Setup(f => f.Delete(_id)).Callback(() => order.Add("files")).Returns(Result.Success());

        var result = await _service.DeleteAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Should().Equal("database", "files");
    }

    [Fact]
    public async Task DeleteAsync_UnknownDocument_ReturnsNotFoundAndDeletesNothing()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.DeleteAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(DeleteFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_DocumentStillProcessing_IsRefusedAndDeletesNothing()
    {
        Existing(DocumentStatus.Processing);

        var result = await _service.DeleteAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(DeleteFailureKind.StillProcessing);
        result.Error.Should().Contain("still being processed");
    }

    [Fact]
    public async Task DeleteAsync_DatabaseFails_KeepsTheFiles()
    {
        Existing(DocumentStatus.Ready);
        _repository.Setup(r => r.DeleteAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure("db down"));

        var result = await _service.DeleteAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(DeleteFailureKind.Internal);
        result.Error.Should().Be("db down");
        _fileStore.Verify(f => f.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_FilesCannotBeDeleted_ReturnsInternalFailure()
    {
        Existing(DocumentStatus.Ready);
        _repository.Setup(r => r.DeleteAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _fileStore.Setup(f => f.Delete(_id)).Returns(Result.Failure("locked"));

        var result = await _service.DeleteAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(DeleteFailureKind.Internal);
        result.Error.Should().Be("locked");
    }

    private void Existing(DocumentStatus status) =>
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = status });
}
