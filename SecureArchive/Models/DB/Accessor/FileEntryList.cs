using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor; 

public interface IFileEntryList {
    IObservable<DataChangeInfo> Changes { get; }
    IList<FileEntry> List();
    IList<FileEntry> List(Func<FileEntry, bool> predicate);
    IList<T> List<T>(Func<FileEntry, bool> predicate, Func<FileEntry, T>select);
    IList<T> List<T>(Func<FileEntry, T?> predicate) where T:class;
    FileEntry? GetById(long id);
    FileEntry? GetByOriginalId(string ownerId, string originalId);
}

public interface IMutableFileEntryList : IFileEntryList {
    FileEntry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? originalId=null, string? metaInfo = null);
    void Remove(FileEntry entry);
    void Remove(Func<FileEntry, bool> predicate);
}

public class DataChangeInfo {
    public enum Change {
        Add,
        Remove,
        //Replace,
        ResetAll,
    }
    public Change Type { get; }
    public FileEntry[] Items { get; private set; }
    DataChangeInfo(Change type, FileEntry[] items) {
        Type = type;
        Items = items;
    }

    public static DataChangeInfo Add(params FileEntry[] entry) {
        return new DataChangeInfo(Change.Add, entry);
    }
    public static DataChangeInfo Remove(params FileEntry[] entry) {
        return new DataChangeInfo(Change.Remove, entry);
    }
    //public static DataChangeInfo Replace(params FileEntry[] entry) {
    //    return new DataChangeInfo(Change.Remove, entry);
    //}
    public static DataChangeInfo ResetAll() {
        return new DataChangeInfo(Change.ResetAll, Array.Empty<FileEntry>());
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

    public IList<FileEntry> List() {
        lock (_connector) {
            return _entries.ToList();
        }
    }
    public IList<FileEntry> List(Func<FileEntry, bool> predicate) {
        lock (_connector) {
            return _entries.Where(predicate).ToList();
        }
    }

    public IList<T> List<T>(Func<FileEntry, bool> predicate, Func<FileEntry, T> select) {
        lock (_connector) {
            return _entries.Where(predicate).Select(select).ToList();
        }
    }
    public IList<T> List<T>(Func<FileEntry, T?> predicate) where T : class {
        lock (_connector) {
            return _entries.Select(predicate).Where(it => it is not null).Select(it => it!).ToList();
        }
    }

    public FileEntry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? originalId=null, string? metaInfo = null) {
        FileEntry entry;
        lock (_connector) {
            entry = new FileEntry { OwnerId = ownerId, OriginalId = originalId, Name = name, Size = size, Type = type, Path = path, MetaInfo = metaInfo, OriginalDate = originalDate, RegisteredDate = DateTime.UtcNow.Ticks };
            _entries.Add(entry);
        }
        _changes.OnNext(DataChangeInfo.Add(entry));
        return entry;
    }

    public void Remove(FileEntry entry) {
        lock (_connector) {
            _entries.Remove(entry);
        }
        _changes.OnNext(DataChangeInfo.Remove(entry));
    }

    public void Remove(Func<FileEntry, bool> predicate) {
        FileEntry[] del;
        lock (_connector) {
            del = _entries.Where(predicate).ToArray();
            _entries.RemoveRange(del);
        }
        _changes.OnNext(DataChangeInfo.Remove(del));
    }

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
