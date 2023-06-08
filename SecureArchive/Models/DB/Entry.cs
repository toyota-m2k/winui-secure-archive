using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;

public class Entry
{
    [Key, Required]
    public long Id { get; set; }
    public string? OriginalId { get; set; }  // Owner App 内でのID
    [Required]
    public string OwnerId { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    [Required]
    public string Type { get; set; } = string.Empty;
    public string? MetaInfo { get; set; }
    [Required]
    public string Path { get; set; } = string.Empty;

    public long RegisteredDate { get; set; }
    public long OriginalDate { get; set; }
}
