using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor; 

public interface IFileEntryList {
    IEnumerable<FileEntry> List();
    IEnumerable<FileEntry> List(Func<FileEntry, bool> predicate);

}
public interface IMutableFileEntryList : IFileEntryList {
    FileEntry Add(string ownerId, string name, long size, string type, string path, long originalDate, string? originalId=null, string? metaInfo = null);
    void Remove(FileEntry entry);
    void Remove(Func<FileEntry, bool> predicate);
}

public class FileEntryList : IMutableFileEntryList {
    private DbSet<FileEntry> _entries;

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
        return entry;
    }

    public void Remove(FileEntry entry) {
        _entries.Remove(entry);
    }

    public void Remove(Func<FileEntry, bool> predicate) {
        var del = _entries.Where(predicate);
        _entries.RemoveRange(del);
    }

}
