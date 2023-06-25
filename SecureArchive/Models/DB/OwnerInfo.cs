using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;

[Table("t_owner_info")]
public class OwnerInfo {
    public static string LOCAL_ID = "LOCAL";
    public static string[] DDL = {
        @"CREATE TABLE IF NOT EXISTS t_owner_info(
            OwnerId TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            Type TEXT NOT NULL,
            Option TEXT,
            Flags INTEGER DEFAULT 0
        )",
    };

    [Key, Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    public string? Option { get; set; }

    public int Flags { get; set; }

    public override bool Equals(object? obj) {
        if (base.Equals(obj)) return true;
        if(obj==null) return false;
        var oi = obj as OwnerInfo;
        if(oi==null) return false;
        return oi.OwnerId == OwnerId && oi.Name == Name && oi.Type == Type && oi.Option == Option && oi.Flags == Flags;
    }

    public override int GetHashCode() {
        return base.GetHashCode();
    }
}
