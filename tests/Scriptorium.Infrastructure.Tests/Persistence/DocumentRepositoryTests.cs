using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Infrastructure.Persistence;

namespace Scriptorium.Infrastructure.Tests.Persistence;

public sealed class DocumentRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DocumentRepository _repository;

    public DocumentRepositoryTests()
    {
        _connection.Open();
        var factory = new InMemoryContextFactory(_connection);
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
        }

        _repository = new DocumentRepository(factory);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsPersistedDocument()
    {
        var document = NewDocument();

        var added = await _repository.AddAsync(document, CancellationToken.None);
        var loaded = await _repository.GetByIdAsync(document.Id, CancellationToken.None);

        added.IsSuccess.Should().BeTrue();
        loaded.Should().NotBeNull();
        loaded.Should().BeEquivalentTo(document);
    }

    [Fact]
    public async Task GetAllAsync_NoDocuments_ReturnsEmptyList()
    {
        var all = await _repository.GetAllAsync(CancellationToken.None);

        all.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDocumentsMostRecentFirst()
    {
        var oldest = NewDocument(uploadDate: new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero));
        var newest = NewDocument(uploadDate: new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
        var middle = NewDocument(uploadDate: new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero));
        foreach (var document in new[] { oldest, newest, middle })
        {
            await _repository.AddAsync(document, CancellationToken.None);
        }

        var all = await _repository.GetAllAsync(CancellationToken.None);

        all.Select(d => d.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task GetAllAsync_KeepsDocumentsWithTheSameFileNameAsSeparateEntries()
    {
        await _repository.AddAsync(NewDocument(), CancellationToken.None);
        await _repository.AddAsync(NewDocument(), CancellationToken.None);

        var all = await _repository.GetAllAsync(CancellationToken.None);

        all.Should().HaveCount(2);
        all.Select(d => d.Id).Distinct().Should().HaveCount(2);
        all.Should().OnlyContain(d => d.FileName == "report.pdf");
    }

    [Fact]
    public async Task FindByContentHashAsync_ReturnsTheDocumentWithThatHash()
    {
        var document = NewDocument(contentHash: "ABC123");
        await _repository.AddAsync(document, CancellationToken.None);
        await _repository.AddAsync(NewDocument(contentHash: "OTHER"), CancellationToken.None);

        var found = await _repository.FindByContentHashAsync("ABC123", CancellationToken.None);

        found!.Id.Should().Be(document.Id);
    }

    [Fact]
    public async Task FindByContentHashAsync_UnknownHash_ReturnsNull()
    {
        await _repository.AddAsync(NewDocument(contentHash: null), CancellationToken.None);

        (await _repository.FindByContentHashAsync("ABC123", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheDocumentAndItsExtractedText()
    {
        var document = NewDocument();
        var other = NewDocument();
        await _repository.AddAsync(document, CancellationToken.None);
        await _repository.AddAsync(other, CancellationToken.None);
        await _repository.SaveExtractedTextAsync(
            new ExtractedText { DocumentId = document.Id, Content = "x", ExtractedAt = DateTimeOffset.UtcNow }, CancellationToken.None);

        var deleted = await _repository.DeleteAsync(document.Id, CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        (await _repository.GetByIdAsync(document.Id, CancellationToken.None)).Should().BeNull();
        (await _repository.GetExtractedTextAsync(document.Id, CancellationToken.None)).Should().BeNull();
        (await _repository.GetByIdAsync(other.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFailure()
    {
        var deleted = await _repository.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        deleted.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var loaded = await _repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_DuplicateId_ReturnsFailure()
    {
        var document = NewDocument();
        await _repository.AddAsync(document, CancellationToken.None);

        var duplicate = await _repository.AddAsync(NewDocument(document.Id), CancellationToken.None);

        duplicate.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStatusAsync_Failed_StoresReason()
    {
        var document = NewDocument();
        await _repository.AddAsync(document, CancellationToken.None);

        var updated = await _repository.UpdateStatusAsync(document.Id, DocumentStatus.Failed, "bad pdf", CancellationToken.None);
        var loaded = await _repository.GetByIdAsync(document.Id, CancellationToken.None);

        updated.IsSuccess.Should().BeTrue();
        loaded!.Status.Should().Be(DocumentStatus.Failed);
        loaded.FailureReason.Should().Be("bad pdf");
    }

    [Fact]
    public async Task UpdateStatusAsync_Ready_ClearsFailureReason()
    {
        var document = NewDocument();
        await _repository.AddAsync(document, CancellationToken.None);

        await _repository.UpdateStatusAsync(document.Id, DocumentStatus.Ready, "ignored", CancellationToken.None);
        var loaded = await _repository.GetByIdAsync(document.Id, CancellationToken.None);

        loaded!.Status.Should().Be(DocumentStatus.Ready);
        loaded.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownId_ReturnsFailure()
    {
        var result = await _repository.UpdateStatusAsync(Guid.NewGuid(), DocumentStatus.Ready, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SaveExtractedTextAsync_ThenGet_ReturnsText()
    {
        var document = NewDocument();
        await _repository.AddAsync(document, CancellationToken.None);
        var text = new ExtractedText { DocumentId = document.Id, Content = "hello", ExtractedAt = DateTimeOffset.UtcNow };

        var saved = await _repository.SaveExtractedTextAsync(text, CancellationToken.None);
        var loaded = await _repository.GetExtractedTextAsync(document.Id, CancellationToken.None);

        saved.IsSuccess.Should().BeTrue();
        loaded!.Content.Should().Be("hello");
    }

    [Fact]
    public async Task SaveExtractedTextAsync_MissingDocument_ReturnsFailure()
    {
        var text = new ExtractedText { DocumentId = Guid.NewGuid(), Content = "x", ExtractedAt = DateTimeOffset.UtcNow };

        var saved = await _repository.SaveExtractedTextAsync(text, CancellationToken.None);

        saved.IsSuccess.Should().BeFalse();
    }

    private static Document NewDocument(Guid? id = null, DateTimeOffset? uploadDate = null, string? contentHash = "HASH") => new()
    {
        Id = id ?? Guid.NewGuid(),
        FileName = "report.pdf",
        FileType = "pdf",
        FileSizeBytes = 1234,
        StoragePath = "/tmp/report.pdf",
        ContentHash = contentHash,
        UploadDate = uploadDate ?? new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
        IsPrivate = true,
        Status = DocumentStatus.Processing
    };

    private sealed class InMemoryContextFactory : IDbContextFactory<ScriptoriumDbContext>
    {
        private readonly SqliteConnection _connection;

        public InMemoryContextFactory(SqliteConnection connection) => _connection = connection;

        public ScriptoriumDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ScriptoriumDbContext>().UseSqlite(_connection).Options;
            return new ScriptoriumDbContext(options);
        }
    }
}
