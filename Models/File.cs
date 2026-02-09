using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("File")]
public class File
{
    [Key]
    public int Id { get; set; }
    
    public string? FileName { get; set; }
    
    public byte[]? HashValue { get; set; }
    
    public long? FileSize { get; set; }
    
    // Navigation properties
    public virtual ICollection<FileChunk> FileChunks { get; set; } = new List<FileChunk>();
    
    public virtual ICollection<FileChunksArchived> FileChunksArchiveds { get; set; } = new List<FileChunksArchived>();
}
