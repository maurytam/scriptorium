using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scriptorium.API.Endpoints;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Services;
using Scriptorium.Infrastructure.Parsing;
using Scriptorium.Infrastructure.Persistence;
using Scriptorium.Infrastructure.Processing;
using Scriptorium.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

var contentRoot = builder.Environment.ContentRootPath;
var connectionString = ResolveConnectionString(builder.Configuration, contentRoot);
var documentsPath = Path.GetFullPath(
    builder.Configuration["Storage:LocalPath"] ?? "App_Data/documents", contentRoot);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContextFactory<ScriptoriumDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<IDocumentRepository, DocumentRepository>();
builder.Services.AddSingleton<IFileStore>(_ => new LocalFileStore(documentsPath));

builder.Services.AddSingleton<IDocumentParser, PdfDocumentParser>();
builder.Services.AddSingleton<IDocumentParser, WordDocumentParser>();
builder.Services.AddSingleton<IDocumentParser, ExcelDocumentParser>();
builder.Services.AddSingleton<IDocumentParser, TextDocumentParser>();

builder.Services.AddSingleton<DocumentProcessingQueue>();
builder.Services.AddSingleton<IDocumentProcessingQueue>(sp => sp.GetRequiredService<DocumentProcessingQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DocumentProcessingQueue>());
builder.Services.AddSingleton<DocumentUploadService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ScriptoriumDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

app.MapDocumentEndpoints();

app.Run();

static string ResolveConnectionString(IConfiguration configuration, string contentRoot)
{
    var configured = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

    var builder = new SqliteConnectionStringBuilder(configured);
    builder.DataSource = Path.GetFullPath(builder.DataSource, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!); // GetFullPath always yields a directory
    return builder.ToString();
}

public partial class Program;
