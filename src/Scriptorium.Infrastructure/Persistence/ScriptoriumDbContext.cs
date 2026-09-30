using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;

namespace Scriptorium.Infrastructure.Persistence;

public sealed class ScriptoriumDbContext : DbContext
{
    public ScriptoriumDbContext(DbContextOptions<ScriptoriumDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<ExtractedText> ExtractedTexts => Set<ExtractedText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureDocument(modelBuilder);
        ConfigureExtractedText(modelBuilder);
    }

    private static void ConfigureDocument(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileName).IsRequired();
            entity.Property(d => d.FileType).IsRequired();
            entity.Property(d => d.StoragePath).IsRequired();
            entity.Property(d => d.Status).HasConversion<string>();
            // Binary conversion keeps DateTimeOffset sortable in SQLite.
            entity.Property(d => d.UploadDate).HasConversion(new DateTimeOffsetToBinaryConverter());
        });
    }

    private static void ConfigureExtractedText(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExtractedText>(entity =>
        {
            entity.HasKey(t => t.DocumentId);
            entity.Property(t => t.Content).IsRequired();
            entity.Property(t => t.ExtractedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.HasOne<Document>()
                .WithOne()
                .HasForeignKey<ExtractedText>(t => t.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
