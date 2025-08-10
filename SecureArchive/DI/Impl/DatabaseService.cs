using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.Globalization;
using System.Xml.Linq;

namespace SecureArchive.DI.Impl;
public class DatabaseService : IDatabaseService, IMutableTables {
    IAppConfigService _appConfigService;
    UtLog _logger = UtLog.Instance(typeof(DatabaseService));
    DBConnector _connector;

    private FileEntryList _entries { get; }
    private OwnerInfoList _ownerList { get; }
    private KVList _kvs { get; }
    private IMutableDeviceMigration _deviceMigration { get; }

    public IFileEntryList Entries => _entries;
    public IOwnerInfoList OwnerList => _ownerList;
    public IKVList KVs => _kvs;
    public IDeviceMigration DeviceMigration => _deviceMigration;

    IMutableFileEntryList IMutableTables.Entries => _entries;
    IMutableOwnerInfoList IMutableTables.OwnerList => _ownerList;
    IMutableKVList IMutableTables.KVs => _kvs;
    IMutableDeviceMigration IMutableTables.DeviceMigration => _deviceMigration;

    public DatabaseService(IAppConfigService appConfigService, ILoggerFactory loggerFactory)
    {
        _appConfigService = appConfigService;
        _logger.Debug(appConfigService.DBPath);

        _connector = new DBConnector(appConfigService.DBPath);
        lock (_connector) {
            _entries = new FileEntryList(_connector);
            _ownerList = new OwnerInfoList(_connector);
            _kvs = new KVList(_connector);
            _deviceMigration = new DeviceMigration(_connector);
        }
        Task.Run(() => {
            //var trial = 0;
            //while(true) {
            //    try {
            //        _ = _connector.Model;
            //        _logger.Debug($"Database connection established. (trial={trial})");
            //        dbReady.TrySetResult(true);
            //        break;
            //    } catch (Exception e) {
            //        _logger.Error(e, $"Database connection failed, retrying {trial}...");
            //        trial++;
            //        if (trial>=30) {
            //            dbReady.TrySetException(e);
            //            throw new Exception("Failed to connect to the database after multiple attempts.", e);
            //        }
            //        await Task.Delay(1000);
            //        continue;
            //    }
            //}
            EditOwnerList(owners => {
                owners.Add(OwnerInfo.LOCAL_ID, "Local", "PC", 0, null);
                return true;
            });
        });

    }

    private IMutableTables mutableTables => this;

    public bool EditEntry(Func<IMutableFileEntryList, bool> fn) {
        bool result = false;
        try {
            lock (_connector) {
                try {
                    result = fn(mutableTables.Entries);
                    return result;
                }
                finally {
                    if (result) {
                        _connector.SaveChanges();
                    }
                }
            }
        }
        finally {
            mutableTables.Entries.ChangeEventSource.Submit();
        }
    }

    public bool EditKVs(Func<IMutableKVList, bool> fn) {
        bool result = false;
        lock (_connector) {
            try {
                result = fn(mutableTables.KVs);
                return result;
            }
            finally {
                if (result) {
                    _connector.SaveChanges();
                }
            }
        }
    }

    public bool EditOwnerList(Func<IMutableOwnerInfoList, bool> fn) {
        bool result = false;
        lock (_connector) {
            try {
                result = fn(mutableTables.OwnerList);
                return result;
            }
            finally {
                if (result) {
                    _connector.SaveChanges();
                }
            }
        }
    }

    public bool EditDeviceMigration(Func<IMutableDeviceMigration, bool> fn) {
        bool result = false;
        lock (_connector) {
            try {
                result = fn(mutableTables.DeviceMigration);
                return result;
            }
            finally {
                if (result) {
                    _connector.SaveChanges();
                }
            }
        }
    }

    public bool Transaction(Func<IMutableTables, bool> fn) {
        bool result = false;
        try {
            lock (_connector) {
                using (var txn = _connector.Database.BeginTransaction()) {
                    try {
                        result = fn(mutableTables);
                    }
                    catch (Exception e) {
                        _logger.Error(e);
                        result = false;
                    }
                    finally {
                        if (result) {
                            _connector.SaveChanges();
                            txn.Commit();
                        }
                        else {
                            txn.Rollback();
                        }
                    }
                }
            }
        } finally {
            if (result) {
                mutableTables.Entries.ChangeEventSource.Submit();
            } else {
                mutableTables.Entries.ChangeEventSource.Reset();
            }
        }
        return result;
    }

    // 実ファイルのないエントリーをDBから削除する

    public (int fromEntries, int fromMigrations) Sweep() {
        (int fromEntries, int fromMigrations) ret = (0, 0);
        lock (_connector) {
            EditEntry(entries => {
                var dels = entries.List().Where(entry => entry.Deleted == 0 && !File.Exists(entry.Path));
                foreach(var del in dels) {
                    entries.Remove(del, deleteDbEntry: true);
                    ret.fromEntries++;
                }
                _logger.Info($"Sweep: {ret.fromEntries} file entries removed.");
                return ret.fromEntries > 0;
            });
            EditDeviceMigration(migrations => {
                var dels = migrations.List().Where((it) => {
                    var entry = Entries.GetByOriginalId(it.NewOwnerId, it.Slot, it.NewOriginalId);
                    return entry == null || (entry.Deleted == 0 && !File.Exists(entry.Path));
                });
                foreach(var del in dels) {
                    migrations.Remove(del);
                    ret.fromMigrations++;
                }
                _logger.Info($"Sweep: {ret.fromMigrations} migration info removed.");
                return ret.fromMigrations > 0;
            });
        }
        return ret;
    }


    public void Update() {
        lock (_connector) {
            _connector.SaveChanges();
        }
    }

    public void Dispose() {
        _logger.Debug("Disposing DatabaseService");
        lock(_connector) {
            _connector.SaveChanges();
            _connector.Dispose();
        }
    }
}
