using Microsoft.EntityFrameworkCore;
using System.Reactive.Subjects;

namespace SecureArchive.Models.DB.Accessor;

public interface IFileEntryList {
    IObservable<DataChangeInfo> Changes { get; }
    IList<FileEntry> List(bool resolveOwnerInfo);
    IList<FileEntry> List(Func<FileEntry, bool> predicate, bool resolveOwnerInfo);
    IList<T> List<T>(Func<FileEntry, bool> predicate, Func<FileEntry, T>select);
    IList<T> List<T>(Func<FileEntry, T?> predicate) where T:class;
    FileEntry? GetById(long id);
    FileEntry? GetByOriginalId(string ownerId, string originalId);
}

public interface IMutableFileEntryList : IFileEntryList {
    FileEntry Add(string ownerId, string name, long size, string type, string path, long originalDate, string originalId, string? metaInfo = null);
    FileEntry Update(string ownerId, string name, long size, string type_, string path, long originalDate, string originalId, string? metaInfo = null);
    FileEntry AddOrUpdate(string ownerId, string name, long size, string type_, string path, long originalDate, string originalId, string? metaInfo = null); 
    void Remove(FileEntry entry);
    //void Remove(Func<FileEntry, bool> predicate);
}

public class DataChangeInfo {
    public enum Change {
        Add,
        Remove,
        Update,
        ResetAll,
    }
    public Change Type { get; }
    public FileEntry Item { get; private set; } = null!;
    DataChangeInfo(Change type, FileEntry item) {
        Type = type;
        Item = item;
    }

    public static DataChangeInfo Add(FileEntry entry) {
        return new DataChangeInfo(Change.Add, entry);
    }
    public static DataChangeInfo Remove(FileEntry entry) {
        return new DataChangeInfo(Change.Remove, entry);
    }
    public static DataChangeInfo Update(FileEntry entry) {
        return new DataChangeInfo(Change.Update, entry);
    }
    public static DataChangeInfo ResetAll() {
        return new DataChangeInfo(Change.ResetAll, new FileEntry());
    }
}



public class FileEntryList : IMutableFileEntryList {
    private DBConnector _connector;
    private DbSet<FileEntry> _entries;
    private Subject<DataChangeInfo> _changes = new();

    public IObservable<DataChangeInfo> Changes => _changes;

    public FileEntryList(DBConnector connector) {
        _connector = connector;
        _entries = connector.Entries;
    }

    private IEnumerable<FileEntry> rawList => _entries.OrderBy(it => it.OriginalDate);

    public IList<FileEntry> List(bool resolveOwnerInfo) {
        lock (_connector) {
            if(resolveOwnerInfo) {
                return ResolveOwnerInfo(rawList);
            } else {
                return rawList.ToList();
            }
        }
    }
    public IList<FileEntry> List(Func<FileEntry, bool> predicate, bool resolveOwnerInfo) {
        lock (_connector) {
            if (resolveOwnerInfo) {
                return ResolveOwnerInfo(rawList.Where(predicate));
            } else {
                return rawList.Where(predicate).ToList();
            }
        }
    }
    private IList<FileEntry> ResolveOwnerInfo(IEnumerable<FileEntry> source) {
        var map = new Dictionary<string, OwnerInfo>();
        lock (_connector) {
            return source.Select(e => {
                if (e.OwnerInfo == null) {
                    if (!map.TryGetValue(e.OwnerId, out var info)) {
                        info = _connector.OwnerInfos.FirstOrDefault(it => it.OwnerId == e.OwnerId) ?? OwnerInfo.Empty;
                        map[e.OwnerId] = info;
                    }
                    e.OwnerInfo = info;
                }
                return e;
            }).ToList();
        }
    }

    private FileEntry ResolveOwnerInfo(FileEntry entry) {
        if (entry.OwnerInfo == null) {
            lock (_connector) {
                entry.OwnerInfo = _connector.OwnerInfos.FirstOrDefault(it => it.OwnerId == entry.OwnerId) ?? OwnerInfo.Empty;
            }
        }
        return entry;
    }

    public IList<T> List<T>(Func<FileEntry, bool> predicate, Func<FileEntry, T> select) {
        lock (_connector) {
            return rawList.Where(predicate).Select(select).ToList();
        }
    }
    public IList<T> List<T>(Func<FileEntry, T?> predicate) where T : class {
        lock (_connector) {
            return rawList.Select(predicate).Where(it => it is not null).Select(it => it!).ToList();
        }
    }

    public FileEntry AddOrUpdate(string ownerId, string name, long size, string type_, string path, long originalDate, string originalId, string? metaInfo = null) {
        if (GetByOriginalId(ownerId, originalId) != null) {
            return Update(ownerId, name, size, type_, path, originalDate, originalId, metaInfo);
        } else {
            return Add(ownerId, name, size, type_, path, originalDate, originalId, metaInfo);
        }
    }
    
    public FileEntry Add(string ownerId, string name, long size, string type_, string path, long originalDate, string originalId, string? metaInfo = null) {
        if(GetByOriginalId(ownerId, originalId) != null) {
            throw new ArgumentException($"already exists: {ownerId}/{originalId}");
        }
        var type = type_;
        if(type_.StartsWith(".")) {
            type = type_.Substring(1);
        }
        FileEntry entry;
        lock (_connector) {
            entry = new FileEntry { OwnerId = ownerId, OriginalId = originalId, Name = name, Size = size, Type = type, Path = path, MetaInfo = metaInfo, OriginalDate = originalDate, RegisteredDate = DateTime.UtcNow.Ticks };
            _entries.Add(entry);
        }
        _changes.OnNext(DataChangeInfo.Add(ResolveOwnerInfo(entry)));
        return entry;
    }

    public FileEntry Update(string ownerId, string name, long size, string type_, string path, long originalDate, string originalId, string? metaInfo = null) {
        var entry = GetByOriginalId(ownerId, originalId);
        if ( entry== null) {
            throw new ArgumentException($"no entry: {ownerId}/{originalId}");
        }
        var type = type_;
        if (type_.StartsWith(".")) {
            type = type_.Substring(1);
        }
        lock (_connector) {
            entry.OwnerId = ownerId;
            entry.Path = path;
            entry.Name = name;
            entry.Size = size;
            entry.Type = type;
            entry.RegisteredDate = DateTime.UtcNow.Ticks;
            entry.OriginalDate = originalDate;
            entry.OriginalId = originalId;
            entry.MetaInfo = metaInfo;
            _entries.Update(entry);
        }
        _changes.OnNext(DataChangeInfo.Update(ResolveOwnerInfo(entry)));
        return entry;

    }

    public void Remove(FileEntry entry) {
        lock (_connector) {
            _entries.Remove(entry);
        }
        _changes.OnNext(DataChangeInfo.Remove(entry));
    }

    //public void Remove(Func<FileEntry, bool> predicate) {
    //    FileEntry[] del;
    //    lock (_connector) {
    //        del = _entries.Where(predicate).ToArray();
    //        _entries.RemoveRange(del);
    //    }
    //    _changes.OnNext(DataChangeInfo.Remove(del));
    //}

    public FileEntry? GetById(long id) {
        lock (_connector) {
            return _entries.Where(it => it.Id == id).SingleOrDefault();
        }
    }

    public FileEntry? GetByOriginalId(string ownerId, string originalId) {
        lock (_connector) {
            return _entries.Where(it => it.OwnerId == ownerId && it.OriginalId == originalId).FirstOrDefault();
        }
    }
}
