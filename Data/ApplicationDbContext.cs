using Microsoft.EntityFrameworkCore;
using FileDownloader.Models;

namespace FileDownloader.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Library> Libraries { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Models.File> Files { get; set; }
    public DbSet<FileChunk> FileChunks { get; set; }
    public DbSet<FileChunksArchived> FileChunksArchiveds { get; set; }
    public DbSet<Setting> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<Library>()
            .HasMany(l => l.Folders)
            .WithOne(f => f.Library)
            .HasForeignKey(f => f.LibraryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Folder>()
            .HasMany(f => f.Documents)
            .WithOne(d => d.Folder)
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Models.File>()
            .HasMany(f => f.FileChunks)
            .WithOne(fc => fc.File)
            .HasForeignKey(fc => fc.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.File>()
            .HasMany(f => f.FileChunksArchiveds)
            .WithOne(fc => fc.File)
            .HasForeignKey(fc => fc.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure indexes for performance
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.FolderId);

        modelBuilder.Entity<FileChunk>()
            .HasIndex(fc => new { fc.FileId, fc.ChunkIndex });

        modelBuilder.Entity<FileChunksArchived>()
            .HasIndex(fc => new { fc.FileId, fc.ChunkIndex });
    }
}
