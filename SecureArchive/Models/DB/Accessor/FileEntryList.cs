using io.github.toyota32k.toolkit.net;
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
    IList<FileEntry> List();
    IList<FileEntry> List(int slot, bool resolveOwnerInfo);
    IList<FileEntry> List(int slot, Func<FileEntry, bool> predicate, bool resolveOwnerInfo);
    IList<T> List<T>(int slot, Func<FileEntry, bool> predicate, Func<FileEntry, T>select);
    IList<T> List<T>(int slot, Func<FileEntry, T?> predicate) where T:class;
    FileEntry? GetById(long id);
    FileEntry? GetByOriginalId(string ownerId, int slot, string originalId);
    List<int> AvailableSlots();
    List<string> AvailableOwnerIds();
}

public interface IDataChangeEventSource {
    void Submit();
    void Reset();
}

public interface IMutableFileEntryList : IFileEntryList {
    FileEntry Add(string ownerId, int slot, string name, long size, string type, string path, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr=null);
    FileEntry Update(string ownerId, int slot, string name, long size, string type_, string path, long lastModifiedDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr = null);
    FileEntry AddOrUpdate(string ownerId, int slot, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr = null); 
    void Remove(FileEntry entry, bool deleteDbEntry = false);
    //void Remove(Func<FileEntry, bool> predicate);
    IDataChangeEventSource ChangeEventSource { get; }
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

class ChangeInfoQueue : IDataChangeEventSource {
    private DBConnector _connector; // DBConnectorは必要ないが、lockのために保持しておく
    private Subject<DataChangeInfo> _changes = new();
    private List<DataChangeInfo> _queue = new List<DataChangeInfo>();
    bool resetAll = false;

    public ChangeInfoQueue(DBConnector connector) {
        _connector = connector;
    }

    public void Enqueue(DataChangeInfo info) {
        lock (_connector) {
            if (info.Type == DataChangeInfo.Change.ResetAll) {
                resetAll = true;
                _queue.Clear();
                return;
            }
            else if (resetAll) {
                // ResetAllが来た後は、Add/Update/Removeは無視する
                return;
            }
            else if (info.Type == DataChangeInfo.Change.Remove) {
                _queue.RemoveAll(it => it.Item.Id == info.Item.Id);
            }
            _queue.Add(info);
        }
    }
    public IObservable<DataChangeInfo> Changes => _changes;

    public void Submit() {
        var all = false;
        List<DataChangeInfo> queue;
        lock (_connector) {
            if (_queue.Count == 0 && !resetAll) {
                return; // 変更がない場合は何もしない
            }
            all = resetAll;
            queue = _queue.ToList(); // キューをコピーしてからクリアする
            _queue.Clear(); // キューをクリア
            resetAll = false; // ResetAllフラグをクリア
        }
        
        // 実際の通知は lock外で行う
        if (all) {
            _changes.OnNext(DataChangeInfo.ResetAll());
            resetAll = false;
        }
        else {
            foreach (var info in queue) {
                _changes.OnNext(info);
            }
        }
    }

    public void Reset() {
        lock (_connector) {
            _queue.Clear();
            resetAll = false; // ResetAllフラグをクリア
        }
    }
}



public class FileEntryList : IMutableFileEntryList {
    private DBConnector _connector;
    private DbSet<FileEntry> _entries;

    private ChangeInfoQueue _changeInfoQueue;
    public IDataChangeEventSource ChangeEventSource => _changeInfoQueue;

    public IObservable<DataChangeInfo> Changes => _changeInfoQueue.Changes;

    //public IObservable<DataChangeInfo> Changes => _changes;

    public FileEntryList(DBConnector connector) {
        _connector = connector;
        _entries = connector.Entries;
        _changeInfoQueue = new ChangeInfoQueue(_connector);
    }

    // private IEnumerable<FileEntry> rawList => _entries.OrderBy(it => it.CreationDate);
    private IEnumerable<FileEntry> rawList(int slot) {
        return _entries.Where(it => slot < 0 || it.Slot == slot).OrderBy(it => it.CreationDate);
    }

    public IList<FileEntry> List() {
        return _entries.ToList();
    }

    public IList<FileEntry> List(int slot, bool resolveOwnerInfo) {
        lock (_connector) {
            try {
                if (resolveOwnerInfo) {
                    return ResolveOwnerInfo(rawList(slot));
                }
                else {
                    return rawList(slot).ToList();
                }
            }
            catch (Exception ex) {
                Logger.error(ex, "Error while listing file entries.");
                return new List<FileEntry>();
            }
        }
    }
    public IList<FileEntry> List(int slot, Func<FileEntry, bool> predicate, bool resolveOwnerInfo) {
        lock (_connector) {
            if (resolveOwnerInfo) {
                return ResolveOwnerInfo(rawList(slot).Where(predicate));
            } else {
                return rawList(slot).Where(predicate).ToList();
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

    public IList<T> List<T>(int slot, Func<FileEntry, bool> predicate, Func<FileEntry, T> select) {
        lock (_connector) {
            return rawList(slot).Where(predicate).Select(select).ToList();
        }
    }
    public IList<T> List<T>(int slot, Func<FileEntry, T?> predicate) where T : class {
        lock (_connector) {
            return rawList(slot).Select(predicate).Where(it => it is not null).Select(it => it!).ToList();
        }
    }

    public FileEntry AddOrUpdate(string ownerId, int slot, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
        if (GetByOriginalId(ownerId, slot, originalId) != null) {
            return Update(ownerId, slot, name, size, type_, path, lastModifiedDate, originalId, duration, metaInfo, extAttr);
        } else {
            return Add(ownerId, slot, name, size, type_, path, lastModifiedDate, creationDate, originalId, duration, metaInfo, extAttr);
        }
    }
    
    public FileEntry Add(string ownerId, int slot, string name, long size, string type_, string path, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
        if(GetByOriginalId(ownerId, slot, originalId) != null) {
            throw new ArgumentException($"already exists: {ownerId}/{slot}/{originalId}");
        }
        // ファイル名から得られる日時が真の creationDate とする（ルール）
        var dateFromName = FileEntry.Filename2UnixTime(name);
        if (dateFromName > 0) {
            creationDate = dateFromName;
        }
        var type = type_;
        if(type_.StartsWith(".")) {
            type = type_.Substring(1);
        }
        FileEntry entry;
        lock (_connector) {
            entry = new FileEntry { 
                OwnerId = ownerId, OriginalId = originalId, Name = name, Size = size, Duration=duration, Slot=slot, Type = type, Path = path, MetaInfo = metaInfo, LastModifiedDate = lastModifiedDate, CreationDate = creationDate, RegisteredDate = DateTime.UtcNow.Ticks,
                ExtAttrDate = extAttr?.ExtAttrDate ?? 0, Rating = extAttr?.Rating ?? 0, Mark = extAttr?.Mark ?? 0, Label = extAttr?.Label, Category = extAttr?.Category, Chapters = extAttr?.Chapters,
            };
            _entries.Add(entry);
        }
        _changeInfoQueue.Enqueue(DataChangeInfo.Add(ResolveOwnerInfo(entry)));
        return entry;
    }

    public FileEntry Update(string ownerId, int slot, string name, long size, string type_, string path, long lastModifiedDate, string originalId, long duration, string? metaInfo = null, IItemExtAttributes? extAttr = null) {
        var entry = GetByOriginalId(ownerId, slot, originalId);
        if (entry == null) {
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
            entry.Duration = duration;
            entry.MetaInfo = metaInfo;
            entry.Slot = slot;
            if (extAttr != null) {
                entry.ExtAttrDate = extAttr.ExtAttrDate;
                entry.Rating = extAttr.Rating;
                entry.Mark = extAttr.Mark;
                entry.Label = extAttr.Label;
                entry.Category = extAttr.Category;
                entry.Chapters = extAttr.Chapters;
            }
            _entries.Update(entry);
        }
        _changeInfoQueue.Enqueue(DataChangeInfo.Update(ResolveOwnerInfo(entry)));
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
        _changeInfoQueue.Enqueue(DataChangeInfo.Remove(entry));
    }

    public FileEntry? GetById(long id) {
        lock (_connector) {
            return _entries.Where(it => it.Id == id).SingleOrDefault();
        }
    }

    public FileEntry? GetByOriginalId(string ownerId, int slot, string originalId) {
        lock (_connector) {
            return _entries.Where(it => it.OwnerId == ownerId && it.Slot == slot && it.OriginalId == originalId).FirstOrDefault();
        }
    }

    public List<int> AvailableSlots() {
        lock (_connector) {
            return _entries.Select(it => it.Slot).Distinct().OrderBy(it => it).ToList();
        }
    }
    public List<string> AvailableOwnerIds() {
        lock (_connector) {
            return _entries.Select(it => it.OwnerId).Distinct().OrderBy(it => it).ToList();
        }
    }

}
