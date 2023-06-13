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
    IEnumerable<FileEntry> List();
    IEnumerable<FileEntry> List(Func<FileEntry, bool> predicate);

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
    private DbSet<FileEntry> _entries;
    private Subject<DataChangeInfo> _changes = new();

    public IObservable<DataChangeInfo> Changes => _changes;

    public FileEntryList(DbSet<FileEntry> entries) {
        _entries = entries;
    }

    public IEnumerable<FileEntry> List() {
        return _entries;
    }
    public IEnumerable<FileEntry> List(Func<FileEntry, bool> predicate) {
        return _entries.Where(predicate);
    }

    public FileEntry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? originalId=null, string? metaInfo = null) {
        var entry = new FileEntry { OwnerId = ownerId, OriginalId = originalId, Name = name, Size = size, Type = type, Path = path, MetaInfo = metaInfo, OriginalDate = originalDate, RegisteredDate = DateTime.UtcNow.Ticks };
        _entries.Add(entry);
        _changes.OnNext(DataChangeInfo.Add(entry));
        return entry;
    }

    public void Remove(FileEntry entry) {
        _entries.Remove(entry);
        _changes.OnNext(DataChangeInfo.Remove(entry));
    }

    public void Remove(Func<FileEntry, bool> predicate) {
        var del = _entries.Where(predicate);
        _entries.RemoveRange(del);
        _changes.OnNext(DataChangeInfo.Remove(del.ToArray()));
    }

}
