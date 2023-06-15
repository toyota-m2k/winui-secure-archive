using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor;
public interface IOwnerInfoList {
    IList<OwnerInfo> List();
    IList<OwnerInfo> List(Func<OwnerInfo, bool> predicate);
    IList<T> List<T>(Func<OwnerInfo, bool> predicate, Func<OwnerInfo, T> select);
    IList<T> List<T>(Func<OwnerInfo, T?> predicate) where T : class;

    OwnerInfo? Get(string ownerId);
}
public interface IMutableOwnerInfoList: IOwnerInfoList {
    OwnerInfo Add(string ownerId, string name, string type, int flag, string? option=null);
    void Remove(OwnerInfo entry);
    void Remove(Func<OwnerInfo, bool> predicate);
}

public class OwnerInfoList : IMutableOwnerInfoList {
    private DBConnector _connector;
    private DbSet<OwnerInfo> _owners;
    public OwnerInfoList(DBConnector connector) {
        _connector = connector;
        _owners = connector.OwnerInfos;
    }

    public OwnerInfo Add(string ownerId, string name, string type, int flag, string? option = null) {
        var owner = new OwnerInfo() {
            OwnerId = ownerId,
            Name = name,
            Type = type,
            Flags = flag,
            Option = option
        };
        lock (_connector) {
            _owners.Add(owner);
        }
        return owner;
    }

    public IList<OwnerInfo> List() {
        lock (_connector) {
            return _owners.ToList();
        }
    }

    public IList <OwnerInfo> List(Func<OwnerInfo, bool> predicate) {
        lock (_connector) {
            return _owners.Where(predicate).ToList();
        }
    }
    public IList<T> List<T>(Func<OwnerInfo, bool> predicate, Func<OwnerInfo, T> select) {
        lock (_connector) {
            return _owners.Where(predicate).Select(select).ToList();
        }
    }
    public IList<T> List<T>(Func<OwnerInfo, T?> predicate) where T : class {
        lock (_connector) {
            return _owners.Select(predicate).Where(it => it is not null).Select(it => it!).ToList();
        }
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
