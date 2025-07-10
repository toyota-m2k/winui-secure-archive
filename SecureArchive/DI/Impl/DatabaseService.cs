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
        Task.Run(async () => {
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
        lock (_connector) {
            using (var txn = _connector.Database.BeginTransaction()) {
                try {
                    result = fn(mutableTables);
                }
                catch(Exception e) {
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
                return result;
            }
        }
    }

    public void Update() {
        lock (_connector) {
            _connector.SaveChanges();
        }
    }
}
