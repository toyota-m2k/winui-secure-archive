﻿using Newtonsoft.Json.Linq;
using SecureArchive.Utils;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
            LastModifiedDate INTEGER DEFAULT 0,
            CreationDate INTEGER DEFAULT 0,
            MetaInfo TEXT,
            Deleted INTEGER DEFAULT 0
        )",
        // FOREIGN KEY(OwnerId) REFERENCES t_owner_info(OwnerId)
    };

    [Key, Required]
    public long Id { get; set; }
    public string OriginalId { get; set; } = string.Empty;  // Owner App 内でのID
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
    public long LastModifiedDate { get; set; }
    public long CreationDate { get; set; }
    public long Deleted { get; set; }

    //[ForeignKey("OwnerId")]
    //public OwnerInfo OwnerInfo { get; set; } = new OwnerInfo();
    [NotMapped]
    public OwnerInfo? OwnerInfo { get; set; } = null;

    [NotMapped]
    public bool IsDeleted => Deleted != 0;

    public Dictionary<string,object> ToDictionary() {
        return new Dictionary<string, object>() {
            { "id", Id },
            { "originalId", OriginalId ?? ""},
            { "ownerId", OwnerId },
            { "name", Name },
            { "size", Size },
            { "type", Type },
            //{ "path", Path },
            { "registeredDate", RegisteredDate },
            { "lastModifiedDate", LastModifiedDate },
            { "creationDate", CreationDate },
            { "metaInfo", MetaInfo ?? "" },
            { "deleted", Deleted },
        };
    }

    public static FileEntry FromDictionary(JObject dict) {
        return new FileEntry() {
            Id = dict.GetIntValue("id"),
            OriginalId = dict.GetStringValue("originalId", ""),
            OwnerId = dict.GetStringValue("ownerId", ""),
            Name = dict.GetStringValue("name", ""),
            Size = dict.GetLongValue("size"),
            Type = dict.GetStringValue("type", ""),
            //Path = (string)dict["path"],
            RegisteredDate = dict.GetLongValue("registeredDate"),
            LastModifiedDate = dict.GetLongValue("lastModifiedDate"),
            CreationDate = dict.GetLongValue("creationDate"),
            MetaInfo = dict.GetStringValue("metaInfo"),
            Deleted = dict.GetIntValue("deleted")
        };
    }
}
