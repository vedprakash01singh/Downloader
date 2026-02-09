using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileDownloader.Models;

[Table("Setting")]
public class Setting
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Value { get; set; } = "";
}
