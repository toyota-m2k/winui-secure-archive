using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor; 

public interface IEntryList {
    IEnumerable<Entry> List();
    IEnumerable<Entry> List(Func<Entry, bool> predicate);

}
public interface IMutableEntryList : IEntryList {
    Entry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? metaInfo = null);
    void Remove(Entry entry);
    void Remove(Func<Entry, bool> predicate);
}

public class EntryList : IMutableEntryList {
    private DbSet<Entry> _entries;

    public EntryList(DbSet<Entry> entries) {
        _entries = entries;
    }

    public IEnumerable<Entry> List() {
        return _entries;
    }
    public IEnumerable<Entry> List(Func<Entry, bool> predicate) {
        return _entries.Where(predicate);
    }

    public Entry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? metaInfo = null) {
        var entry = new Entry { OwnerId = ownerId, Name = name, Size = size, Type = type, Path = path, MetaInfo = metaInfo, OriginalDate = originalDate, RegisteredDate = DateTime.UtcNow.Ticks };
        _entries.Add(entry);
        return entry;
    }

    public void Remove(Entry entry) {
        _entries.Remove(entry);
    }

    public void Remove(Func<Entry, bool> predicate) {
        var del = _entries.Where(predicate);
        _entries.RemoveRange(del);
    }

}
