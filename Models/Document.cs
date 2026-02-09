using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("Documents")]
public class Document
{
    [Key]
    public int ID { get; set; }
    
    public int FolderId { get; set; }
    
    public int FileId { get; set; }
    
    [Column("Name")]
    public string? Name { get; set; }
    
    [Column("Description")]
    public string? Description { get; set; }
    
    [Column("PhysicalPath")]
    public string? PhysicalPath { get; set; }
    
    [Column("Tags")]
    public string? Tags { get; set; }
    
    [Column("Deleted")]
    public bool Deleted { get; set; }
    
    [Column("IsArchived")]
    public Nullable<byte> IsArchived { get; set; }
    
    [Column("CreatedOn")]
    public DateTime? CreatedOn { get; set; }
    
    // Navigation properties
    [ForeignKey("FolderId")]
    public virtual Folder? Folder { get; set; }
    
    [ForeignKey("FileId")]
    public virtual File? File { get; set; }
}
