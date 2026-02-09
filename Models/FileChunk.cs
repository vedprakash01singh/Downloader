using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("FileChunks")]
public class FileChunk
{
    [Key]
    public int Id { get; set; }
    
    public int FileId { get; set; }
    
    public int ChunkIndex { get; set; }
    
    public byte[]? ChunkData { get; set; }
    
    public long ChunkSize { get; set; }
    
    // Navigation properties
    [ForeignKey("FileId")]
    public virtual File? File { get; set; }
}
