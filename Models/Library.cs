using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("Library")]
public class Library
{
    [Key]
    public int ID { get; set; }
    
    public string? LibraryName { get; set; }
    
    public string? Description { get; set; }
    
    public bool Deleted { get; set; }
    
    public DateTime? CreatedOn { get; set; }
    
    public DateTime? UpdatedOn { get; set; }
    
    // Navigation properties
    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();
}
