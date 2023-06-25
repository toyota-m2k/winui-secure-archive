using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;

[Table("t_entry")]
public class FileEntry {
    public static string[] DDL = {
        @"CREATE TABLE IF NOT EXISTS t_entry (
            Id INTEGER NOT NULL PRIMARY KEY,
            OriginalId TEXT,
            OwnerId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Size INTEGER DEFAULT 0,
            Type TEXT NOT NULL,
            Path TEXT NOT NULL,
            RegisteredDate INTEGER DEFAULT 0,
            OriginalDate INTEGER DEFAULT 0,
            MetaInfo TEXT,
            FOREIGN KEY(OwnerId) REFERENCES t_owner_info(OwnerId)
        )",
    };

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

    [ForeignKey("OwnerId")]
    public OwnerInfo OwnerInfo { get; set; } = new OwnerInfo();
}
