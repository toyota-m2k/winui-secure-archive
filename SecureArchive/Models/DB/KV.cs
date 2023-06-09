using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;
[Table("t_kv")]
public class KV {
    public static string[] DDL = {
        @"CREATE TABLE IF NOT EXISTS t_kv(
            Key TEXT NOT NULL PRIMARY KEY,
            sValue TEXT,
            iValue INTEGER DEFAULT 0
        )",
    };

    [Key, Required]
    public string Key { get; set; } = string.Empty;
    public string? sValue { get; set; }
    public int iValue { get; set; }
}
