using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor;
public interface IKVList {
    int GetInt(string key, int def = 0);
    string? GetString(string key);
}

public interface IMutableKVList : IKVList {
    void SetInt(string key, int value);
    void SetString(string key, string value);
    void Delete(string key);
}

public class KVList: IMutableKVList {
    private DBConnector _connector;
    private DbSet<KV> _kvs;
    public KVList(DBConnector connector) {
        _connector = connector;
        _kvs = connector.KVs;
    }

    public void Delete(string key) {
        lock (_connector) {
            _kvs.RemoveRange(_kvs.Where(it => it.Key == key));
        }
    }

    public int GetInt(string key, int def=0) {
        lock (_connector) {
            return _kvs.FirstOrDefault(it => it.Key == key)?.iValue ?? 0;
        }
    }

    public string? GetString(string key) {
        lock (_connector) {
            return _kvs.FirstOrDefault(it => it.Key == key)?.sValue;
        }
    }

    public void SetInt(string key, int value) {
        lock (_connector) {
            var e = _kvs.FirstOrDefault(it => it.Key == key);
            if (e != null) {
                e.iValue = value;
            }
            else {
                _kvs.Add(new KV() {
                    Key = key,
                    iValue = value
                });
            }
        }
    }

    public void SetString(string key, string value) {
        lock (_connector) {
            var e = _kvs.FirstOrDefault(it => it.Key == key);
            if (e != null) {
                e.sValue = value;
            }
            else {
                _kvs.Add(new KV() {
                    Key = key,
                    sValue = value
                });
            }
        }
    }
}
