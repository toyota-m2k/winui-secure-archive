using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace SecureArchive.Models.DB.Accessor;
public interface IOwnerInfoList {
    IList<OwnerInfo> List();
    IList<OwnerInfo> List(Func<OwnerInfo, bool> predicate);
    IList<T> List<T>(Func<OwnerInfo, bool> predicate, Func<OwnerInfo, T> select);
    IList<T> List<T>(Func<OwnerInfo, T?> predicate) where T : class;

    OwnerInfo? Get(string ownerId);
    string JsonForSync();
}
public interface IMutableOwnerInfoList: IOwnerInfoList {
    bool Add(string ownerId, string name, string type, int flag, string? option=null);
    bool AddOrUpdate(string ownerId, string name, string type, int flag, string? option = null);
    void Remove(OwnerInfo entry);
    void Remove(Func<OwnerInfo, bool> predicate);
    bool SyncByJson(string jsonString);
}

public class OwnerInfoList : IMutableOwnerInfoList {
    private DBConnector _connector;
    private DbSet<OwnerInfo> _owners;
    public OwnerInfoList(DBConnector connector) {
        _connector = connector;
        _owners = connector.OwnerInfos;
    }

    public bool Add(string ownerId, string name, string type, int flag, string? option = null) {
        lock (_connector) {
            var org = Get(ownerId);
            if (org == null) {
                var owner = new OwnerInfo() {
                    OwnerId = ownerId,
                    Name = name,
                    Type = type,
                    Flags = flag,
                    Option = option
                };
                _owners.Add(owner);
                return true;
            }
        }
        return false;

    }

    public bool AddOrUpdate(string ownerId, string name, string type, int flag, string? option = null) {
        var owner = new OwnerInfo() {
            OwnerId = ownerId,
            Name = name,
            Type = type,
            Flags = flag,
            Option = option
        };
        lock (_connector) {
            var org = Get(ownerId);
            if (org == null) {
                _owners.Add(owner);
                return true;
            } else if(owner!=org) {
                org.Name = name;
                org.Type = type;
                org.Flags = flag;
                org.Option = option;
                _owners.Update(org);
                return true;
            } else {
                return false;
            }
        }
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
        lock (_connector) {
            return _owners.FirstOrDefault(it => it.OwnerId == ownerId);
        }
    }

    public void Remove(OwnerInfo entry) {
        lock (_connector) {
            _owners.Remove(entry);
        }
    }
    public void Remove(Func<OwnerInfo, bool> predicate) {
        lock (_connector) {
            var del = _owners.Where(predicate);
            _owners.RemoveRange(del);
        }
    }

    public string JsonForSync() {
        var owners = List<Dictionary<string, object>>(
            predicate: owner => {
                return owner.Type != "PC";
            },
            select: owner => {
                return owner.ToDictionary();
            }).ToList();
        return JsonConvert.SerializeObject(new Dictionary<string, object> {
                { "cmd", "sync/owners" },
                { "list", owners }
            });
    }

    public bool SyncByJson(string jsonString) {
        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        if (json != null && json.ContainsKey("list")) {
            var list = json["list"] as JArray;
            if (list != null) {
                var modified = false;
                foreach (JObject owner in list) {
                    var ownerInfo = OwnerInfo.FromDictionary(owner);
                    if (!string.IsNullOrEmpty(ownerInfo.OwnerId) && Get(ownerInfo.OwnerId) == null) {
                        Add(ownerInfo.OwnerId, ownerInfo.Name, ownerInfo.Type, ownerInfo.Flags, ownerInfo.Option);
                        modified = true;
                    }
                }
                return modified;
            }
        }
        return false;
    }
}
