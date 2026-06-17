using Newtonsoft.Json.Linq;
using SecureArchive.Utils;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureArchive.Models.DB;

[Table("t_migration")]
public class DeviceMigrationInfo {
    public static string[] DDL = {
        @"CREATE TABLE IF NOT EXISTS t_migration(
            Key INTEGER NOT NULL PRIMARY KEY,
            OldOriginalId TEXT NOT NULL,
            OldOwnerId TEXT NOT NULL,
            Slot INTEGER DEFAULT 0,
            NewOriginalId TEXT NOT NULL,
            NewOwnerId TEXT NOT NULL,
            MigratedOn INTEGER NOT NULL
        )",
    };

    private static string[] Migrate0_3 = {
        @"ALTER TABLE t_migration ADD Slot INTEGER DEFAULT 0",
    };
    public static string[]? Migrate(long from, long to) {
        if (from < 3) {
            return Migrate0_3;
        } else {
            return null;
        }
    }

    [Key, Required]
    public long Key { get; set; }
    [Required]
    public string OldOriginalId { get; set; } = string.Empty;   // 旧デバイス上でのID
    [Required]
    public string OldOwnerId { get; set; } = string.Empty;      // 旧デバイスのオーナーID
    [Required]
    public int Slot { get; set; } = 0;                          // スロット番号：移行元と移行先が同じスロット構成であることを前提とする。（スロットのマッピングは行わない）
    [Required]
    public string NewOriginalId { get; set; } = string.Empty;   // 新デバイス上でのID
    [Required]
    public string NewOwnerId { get; set; } = string.Empty;      // 新デバイスのオーナーID
    public long MigratedOn { get; set; }                        // 移行日時（Ticks）

    public Dictionary<string, object> ToDictionary() {
        return new Dictionary<string, object>() {
            { "Key", Key },
            { "OldOriginalId", OldOriginalId },
            { "OldOwnerId", OldOwnerId },
            { "Slot", Slot },
            { "NewOriginalId", NewOriginalId },
            { "NewOwnerId", NewOwnerId },
            { "MigratedOn", MigratedOn }
        };
    }
    public static DeviceMigrationInfo FromDictionary(JObject dict) {
        return new DeviceMigrationInfo() {
            Key = dict.GetLongValue("Key"),
            OldOriginalId = dict.GetStringValue("OldOriginalId", ""),
            OldOwnerId = dict.GetStringValue("OldOwnerId", ""),
            Slot = dict.GetIntValue("Slot", 0),
            NewOriginalId = dict.GetStringValue("NewOriginalId", ""),
            NewOwnerId = dict.GetStringValue("NewOwnerId", ""),
            MigratedOn = dict.GetLongValue("MigratedOn")
        };
    }
}
