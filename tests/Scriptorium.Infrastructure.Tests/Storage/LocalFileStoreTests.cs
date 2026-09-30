using System.Text;
using FluentAssertions;
using Scriptorium.Infrastructure.Storage;

namespace Scriptorium.Infrastructure.Tests.Storage;

public sealed class LocalFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scriptorium-tests", Guid.NewGuid().ToString());
    private readonly LocalFileStore _store;

    public LocalFileStoreTests() => _store = new LocalFileStore(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesBytesUnderDocumentDirectory()
    {
        var id = Guid.NewGuid();
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        var result = await _store.SaveAsync(id, "notes.txt", content, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Path.Combine(_root, id.ToString(), "notes.txt"));
        (await File.ReadAllTextAsync(result.Value)).Should().Be("payload");
    }

    [Fact]
    public async Task SaveAsync_StripsDirectoryComponentsFromFileName()
    {
        var id = Guid.NewGuid();
        using var content = new MemoryStream([1, 2, 3]);

        var result = await _store.SaveAsync(id, "../../evil.txt", content, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().StartWith(Path.Combine(_root, id.ToString()));
        Path.GetFileName(result.Value).Should().Be("evil.txt");
    }

    [Fact]
    public async Task SaveAsync_SameDocumentIdAndName_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        await _store.SaveAsync(id, "a.txt", new MemoryStream([1]), CancellationToken.None);

        var second = await _store.SaveAsync(id, "a.txt", new MemoryStream([2]), CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OpenRead_ExistingFile_ReturnsReadableStream()
    {
        var saved = await _store.SaveAsync(Guid.NewGuid(), "a.txt", new MemoryStream(Encoding.UTF8.GetBytes("abc")), CancellationToken.None);

        var opened = _store.OpenRead(saved.Value!);

        opened.IsSuccess.Should().BeTrue();
        Assert.NotNull(opened.Value);
        await using var stream = opened.Value;
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be("abc");
    }

    [Fact]
    public void OpenRead_MissingFile_ReturnsFailure()
    {
        var opened = _store.OpenRead(Path.Combine(_root, "missing.txt"));

        opened.IsSuccess.Should().BeFalse();
    }
}
