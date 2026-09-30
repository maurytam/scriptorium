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

    private static Document NewDocument(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FileName = "report.pdf",
        FileType = "pdf",
        FileSizeBytes = 1234,
        StoragePath = "/tmp/report.pdf",
        UploadDate = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
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
