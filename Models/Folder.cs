using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("Folders")]
public class Folder
{
    [Key]
    public int ID { get; set; }
    
    public int LibraryId { get; set; }
    
    public string? FolderName { get; set; }
    
    public string? PhysicalPath { get; set; }
    
    public bool Deleted { get; set; }
    
    public DateTime? CreatedOn { get; set; }
    
    // Navigation properties
    [ForeignKey("LibraryId")]
    public virtual Library? Library { get; set; }
    
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
