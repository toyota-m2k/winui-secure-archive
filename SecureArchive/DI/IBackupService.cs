using Newtonsoft.Json;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

//public class ItemExtAttributes : IItemExtAttributes {
//    [JsonProperty("cmd")]
//    public string Cmd { get; set; } = "";
//    [JsonProperty("id")]
//    public string Id { get; set; } = "";
//    [JsonProperty("attrDate")]
//    public long ExtAttrDate { get; set; } = 0;
//    [JsonProperty("rating")]
//    public int Rating { get; set; } = 0;
//    [JsonProperty("mark")]
//    public int Mark { get; set; } = 0;
//    [JsonProperty("label")]
//    public string? Label { get; set; } = "";
//    [JsonProperty("category")]
//    public string? Category { get; set; } = "";
//    [JsonProperty("chapters")]
//    public string? Chapters { get; set; } = "";

//}


internal interface IBackupService {
    public enum Status {
        NONE,
        LISTING,
        DOWNLOADING,
    }

    /**
     * リモート（スマホ）側で追加され、SecureArchive未登録のアイテムリスト
     */
    IList<RemoteItem> RemoteNewItems { get; }
    /**
     * リモート（スマホ）側で削除され、SequreArchiveに残っているアイテムのリスト
     */
    IList<FileEntry> RemoteRemovedItems { get; }
    /**
     * リモート（スマホ）側で属性(ExtraAttributes)が変更され、SequreArchiveに反映されていないアイテムのリスト
     */
    IList<FileEntry> RemoteModifiedItems { get; }

    bool Request(string ownerId, string token, string url);
    Task<bool> DownloadTarget(RemoteItem item, ProgressProc progress, CancellationToken ct);
    Task<bool> DeleteBackupEntry(FileEntry entry);
    Task<bool> UpdateBackupEntry(FileEntry entry, CancellationToken ct);
    bool RequestDBBackup(string ownerId, string token, string url);
}
