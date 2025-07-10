using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureArchive.Models.DB;

[Table("t_entry")]
public class FileEntry : IItemExtAttributes {
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
            Deleted INTEGER DEFAULT 0,
            ExtAttrDate INTEGER DEFAULT 0,
            Rating INTEGER DEFAULT 0,
            Mark INTEGER DEFAULT 0,
            Label TEXT,
            Category TEXT,
            Chapters TEXT,
            Duration INTEGER DEFAULT 0,
            Slot INTEGER DEFAULT 0
        )",

        // FOREIGN KEY(OwnerId) REFERENCES t_owner_info(OwnerId)
    };

    private static string[] Migrate0_1 = {
        @"ALTER TABLE t_entry ADD ExtAttrDate INTEGER DEFAULT 0",
        @"ALTER TABLE t_entry ADD Rating INTEGER DEFAULT 0",
        @"ALTER TABLE t_entry ADD Mark INTEGER DEFAULT 0",
        @"ALTER TABLE t_entry ADD Label TEXT",
        @"ALTER TABLE t_entry ADD Category TEXT",
        @"ALTER TABLE t_entry ADD Chapters TEXT",
    };
    private static string[] Migrate1_2 = {
        @"ALTER TABLE t_entry ADD Duration INTEGER DEFAULT 0",
    };
    private static string[] Migrate2_3 = {
        @"ALTER TABLE t_entry ADD Slot INTEGER DEFAULT 0",
    };

    public static string[]? Migrate(long from, long to) {
        if (from < 1) {
            return Migrate0_1.Concat(Migrate1_2).Concat(Migrate2_3).ToArray();
        } else if(from<2) {
            return Migrate1_2.Concat(Migrate2_3).ToArray();
        } else if (from < 3) {
            return Migrate2_3;
        }
        else {
            return null;
        }
    }

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
    public long ExtAttrDate { get; set;}
    // 以下、extended attributes
    public int Rating { get; set; }
    public int Mark { get; set; }
    public string? Label { get; set; }
    public string? Category { get; set; }
    public string? Chapters { get; set; }
    public long Duration { get; set; } = 0;
    public int Slot { get; set; } = 0;

    //[ForeignKey("OwnerId")]
    //public OwnerInfo OwnerInfo { get; set; } = new OwnerInfo();
    [NotMapped]
    public OwnerInfo? OwnerInfo { get; set; } = null;

    [NotMapped]
    public bool IsDeleted => Deleted != 0;

    [NotMapped]
    public int CorrectiveRating => Rating == 3 ? 0 : Rating;

    [NotMapped]
    public string MediaType => Type == "mp4" ? "v" : "p";
    [NotMapped]
    public string ContentType => Type == "mp4" ? "video/mp4" : Type=="png" ? "image/png" : "image/jpeg";


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

            { "extAttrDate", ExtAttrDate },
            { "rating", Rating },
            { "mark", Mark },
            { "label", Label ?? "" },
            { "category", Category ?? "" },
            { "chapters", Chapters ?? "" },
            { "duration", Duration },
            { "slot", Slot },
        };
    }

    public Dictionary<string,object> AttrDataDic {
        get {
            return new Dictionary<string, object> {
                { "extAttrDate", ExtAttrDate },
                { "rating", Rating },
                { "mark", Mark },
                { "label", Label ?? "" },
                { "category", Category ?? "" },
                { "chapters", Chapters ?? "" },
            };
        }
    }

    public string AttrDataJson {
        get {
            return JsonConvert.SerializeObject(AttrDataDic);
        }
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
            Deleted = dict.GetIntValue("deleted"),

            ExtAttrDate = dict.GetLongValue("extAttrDate"),
            Rating = dict.GetIntValue("rating"),
            Mark = dict.GetIntValue("mark"),
            Label = dict.GetStringValue("label"),
            Category = dict.GetStringValue("category"),
            Chapters = dict.GetStringValue("chapters"),
            Duration = dict.GetLongValue("duration"),
            Slot = dict.GetIntValue("slot"),
        };
    }

}
