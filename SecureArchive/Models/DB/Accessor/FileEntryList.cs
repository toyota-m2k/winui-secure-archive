using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SecureArchive.DI;
using SecureArchive.Utils;
using System.Reactive.Subjects;

namespace SecureArchive.Models.DB.Accessor;

public interface IItemExtAttributes {
    long ExtAttrDate { get; set; }
    int Rating { get; set; }
    int Mark { get; set; }
    string? Label { get; set; }
    string? Category { get; set; }
    string? Chapters { get; set; }
}

public class ItemExtAttributes : IItemExtAttributes {
    private IDictionary<string, object> _dic;
    public ItemExtAttributes(IDictionary<string,object> dic) {
        _dic = dic;
    }

    public long ExtAttrDate { 
        get => _dic.GetLong("attrDate"); 
        set => _dic["attrDate"] = value; 
    }

    public int Rating { 
        get => _dic.GetInt("rating");
        set => _dic["rating"] = value; 
    }
    public int Mark {
        get => _dic.GetInt("mark");
        set => _dic["mark"] = value;
    }
    public string? Label {
        get => _dic.GetNullableString("label");
        set {
            if(value == null) {
                _dic.Remove("label");
            } else {
                _dic["label"] = value;
            }
        }
    }
    public string? Category {
        get => _dic.GetNullableString("category");
        set {
            if (value == null) {
                _dic.Remove("category");
            } else {
                _dic["category"] = value;
            }
        }
    }
    public string? Chapters {
        get => _dic.GetNullableString("chapters");
        set {
            if (value == null) {
                _dic.Remove("chapters");
            } else {
                _dic["chapters"] = value;
            }
        }
    }

    public static ItemExtAttributes FromDic(IDictionary<string, object> dic) {
        return new ItemExtAttributes(dic);
    }

    public static ItemExtAttributes FromJson(string json) {
        IDictionary<string, object> dic = JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        return FromDic(dic);
    }

    public static ItemExtAttributes Duplicate(IItemExtAttributes ext) {
        return new ItemExtAttributes(new Dictionary<string, object>() {
            { "attrDate", ext.ExtAttrDate },
            { "rating", ext.Rating },
            { "mark", ext.Mark },
            { "label", ext.Label ?? "" },
            { "category", ext.Category ?? "" },
            { "chapters", ext.Chapters ?? "" },
        });
    }

    public string ToJson() {
        return JsonConvert.SerializeObject(_dic);
    }
    public string ToJson(IDictionary<string, object> ext) {
        var dic = new Dictionary<string, object>(_dic);
        foreach(var kv in ext) {
            dic[kv.Key] = kv.Value;
        }
        return JsonConvert.SerializeObject(dic);
    }
}


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
    FileEntry Add(string ownerId, string name, long size, string type, string path, long lastModifiedDate, long creationDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr=null);
    FileEntry Update(string ownerId, string name, long size, string type_, string path, long lastModifiedDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr = null);
    FileEntry AddOrUpdate(string ownerId, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr = null); 
    void Remove(FileEntry entry, bool deleteDbEntry = false);
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
    private Subject<DataChangeInfo> _changes = new ();

    public IObservable<DataChangeInfo> Changes => _changes;

    public FileEntryList(DBConnector connector) {
        _connector = connector;
        _entries = connector.Entries;
    }

    private IEnumerable<FileEntry> rawList => _entries.OrderBy(it => it.Name);

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

    public FileEntry AddOrUpdate(string ownerId, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
        if (GetByOriginalId(ownerId, originalId) != null) {
            return Update(ownerId, name, size, type_, path, lastModifiedDate, originalId, metaInfo, extAttr);
        } else {
            return Add(ownerId, name, size, type_, path, lastModifiedDate, creationDate, originalId, metaInfo, extAttr);
        }
    }
    
    public FileEntry Add(string ownerId, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
        if(GetByOriginalId(ownerId, originalId) != null) {
            throw new ArgumentException($"already exists: {ownerId}/{originalId}");
        }
        var type = type_;
        if(type_.StartsWith(".")) {
            type = type_.Substring(1);
        }
        FileEntry entry;
        lock (_connector) {
            entry = new FileEntry { 
                OwnerId = ownerId, OriginalId = originalId, Name = name, Size = size, Type = type, Path = path, MetaInfo = metaInfo, LastModifiedDate = lastModifiedDate, CreationDate = creationDate, RegisteredDate = DateTime.UtcNow.Ticks,
                ExtAttrDate = extAttr?.ExtAttrDate ?? 0, Rating = extAttr?.Rating ?? 0, Mark = extAttr?.Mark ?? 0, Label = extAttr?.Label, Category = extAttr?.Category, Chapters = extAttr?.Chapters,
            };
            _entries.Add(entry);
        }
        _changes.OnNext(DataChangeInfo.Add(ResolveOwnerInfo(entry)));
        return entry;
    }

    public FileEntry Update(string ownerId, string name, long size, string type_, string path, long lastModifiedDate, string originalId, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
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
            entry.LastModifiedDate = lastModifiedDate;
            entry.OriginalId = originalId;
            entry.MetaInfo = metaInfo;
            if(extAttr != null) {
                entry.ExtAttrDate = extAttr.ExtAttrDate;
                entry.Rating = extAttr.Rating;
                entry.Mark = extAttr.Mark;
                entry.Label = extAttr.Label;
                entry.Category = extAttr.Category;
                entry.Chapters = extAttr.Chapters;
            }
            _entries.Update(entry);
        }
        _changes.OnNext(DataChangeInfo.Update(ResolveOwnerInfo(entry)));
        return entry;

    }

    public void Remove(FileEntry entry, bool deleteDbEntry) {
        lock (_connector) {
            if (deleteDbEntry) {
                _entries.Remove(entry);
            } else {
                if (entry.Deleted == 0) {
                    entry.Deleted = 1;
                    _entries.Update(entry);
                }
            }
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
