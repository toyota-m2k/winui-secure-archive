using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor;
public interface IOwnerInfoList {
    IEnumerable<OwnerInfo> List();
    IEnumerable<OwnerInfo> List(Func<OwnerInfo, bool> predicate);

    OwnerInfo? Get(string ownerId);
}
public interface IMutableOwnerInfoList: IOwnerInfoList {
    OwnerInfo Add(string ownerId, string name, string type, int flag, string? option=null);
    void Remove(OwnerInfo entry);
    void Remove(Func<OwnerInfo, bool> predicate);
}

public class OwnerInfoList : IMutableOwnerInfoList {
    private DbSet<OwnerInfo> _owners;
    public OwnerInfoList(DbSet<OwnerInfo> owners) {
        _owners = owners;
    }
    public OwnerInfo Add(string ownerId, string name, string type, int flag, string? option = null) {
        var owner = new OwnerInfo() {
            OwnerId = ownerId,
            Name = name,
            Type = type,
            Flags = flag,
            Option = option
        };
        _owners.Add(owner);
        return owner;
    }

    public IEnumerable<OwnerInfo> List() {
        return _owners;
    }

    public IEnumerable<OwnerInfo> List(Func<OwnerInfo, bool> predicate) {
        return _owners.Where(predicate);
    }

    public OwnerInfo? Get(string ownerId) {
        return _owners.FirstOrDefault(it => it.OwnerId == ownerId);
    }

    public void Remove(OwnerInfo entry) {
        _owners.Remove(entry);
    }
    public void Remove(Func<OwnerInfo, bool> predicate) {
        var del = _owners.Where(predicate);
        _owners.RemoveRange(del);
    }

}
