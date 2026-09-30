using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Scriptorium.Infrastructure.Tests.Support;

/// <summary>Runs the API in-process against a throwaway database and file folder.</summary>
internal sealed class ApiTestHost : IDisposable
{
    public ApiTestHost()
    {
        DataPath = Path.Combine(Path.GetTempPath(), "scriptorium-tests", Guid.NewGuid().ToString());
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(DataPath, "test.db")}");
            builder.UseSetting("Storage:LocalPath", Path.Combine(DataPath, "documents"));
        });
        Client = Factory.CreateClient();
    }

    public string DataPath { get; }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Client { get; }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public Task<HttpResponseMessage> UploadAsync(string fileName, byte[] bytes, string? isPrivate = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        if (isPrivate is not null)
        {
            form.Add(new StringContent(isPrivate), "isPrivate");
        }

        return Client.PostAsync("/api/documents", form);
    }

    /// <summary>Uploads a file and returns the id of the accepted document.</summary>
    public async Task<Guid> UploadAcceptedAsync(string fileName, byte[] bytes, string? isPrivate = null)
    {
        var response = await UploadAsync(fileName, bytes, isPrivate);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    public async Task<JsonElement> WaitForTerminalStatusAsync(Guid id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var details = await ReadJsonAsync(await Client.GetAsync($"/api/documents/{id}"));
            if (details.GetProperty("status").GetString() != "processing")
            {
                return details;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Document {id} did not reach a terminal status.");
    }

    public string[] StoredDocumentFolders()
    {
        var documents = Path.Combine(DataPath, "documents");
        return Directory.Exists(documents) ? Directory.GetDirectories(documents) : [];
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(DataPath))
        {
            Directory.Delete(DataPath, recursive: true);
        }
    }
}
