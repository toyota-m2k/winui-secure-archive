using Newtonsoft.Json;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;


public interface IEntryCreator : IDisposable {
    Stream OutputStream { get; }
    FileEntry Complete(string name, long size, string type, long lastModifiedDate, long creationDate, long duration, string? metaInfo, IItemExtAttributes? extAttr);
}

internal interface ISecureStorageService {
    bool IsRegistered(string ownerId, string originalId);
    bool IsRegistered(string ownerId, string originalId, long lastModified);
    IList<FileEntry> GetList(string ownerId, Func<FileEntry, bool>? predicate);
    Task<FileEntry?> RegisterFile(string filePath, string ownerId, string? name, string originalId, long duration, string? metaInfo, ProgressProc? progress);
    Task<FileEntry?> Register(Stream inStream, string ownerId, string name, long size, string type, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo, ProgressProc? progress);
    Task<IEntryCreator?> CreateEntry(string ownerId, string originalId, bool overwrite=false);

    Stream OpenEntry(FileEntry entry);
    Task Export(FileEntry entry, string outPath);
    Task Export(FileEntry entry, string outPath, ProgressProc progress);

    Task<bool> SetStorageFolder(string newPath);

    Task<bool> DeleteEntry(FileEntry entry, bool deleteDbEntry=false);

    Task ConvertFastStart(IStatusNotificationService notificationService, List<FileEntry> entries);
    Task CheckFastStart(IStatusNotificationService notificationService, List<FileEntry> entries);

}
