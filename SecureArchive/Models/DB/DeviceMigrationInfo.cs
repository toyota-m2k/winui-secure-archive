using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;

[Table("t_migration")]
public class DeviceMigrationInfo {
    public static string[] DDL = {
        @"CREATE TABLE IF NOT EXISTS t_migration(
            Key INTEGER NOT NULL PRIMARY KEY,
            OldOriginalId TEXT NOT NULL,
            OldOwnerId TEXT NOT NULL,
            NewOriginalId TEXT NOT NULL,
            NewOwnerId TEXT NOT NULL,
            MigratedOn INTEGER NOT NULL
        )",
    };

    [Key, Required]
    public long Key { get; set; }
    [Required]
    public string OldOriginalId { get; set; } = string.Empty;  // Owner App 内でのID
    [Required]
    public string OldOwnerId { get; set; } = string.Empty;
    [Required]
    public string NewOriginalId { get; set; } = string.Empty;  // Owner App 内でのID
    [Required]
    public string NewOwnerId { get; set; } = string.Empty;
    public long MigratedOn { get; set; }
}
