using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.Globalization;

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

        //EditEntry((entries) => {
        //    bool modified = false;
        //    string csharpFormat = "yyyy.MM.dd-HH:mm:ss";
        //    foreach (var entry in entries.List(false)) {
        //        if (/*entry.CreationDate == 0 && */ (entry.Name.StartsWith("mov-") || entry.Name.StartsWith("img-"))) {
        //            var timeText = entry.Name.Substring(4, entry.Name.Length - 8);
        //            var dt = DateTime.ParseExact(timeText, csharpFormat, CultureInfo.InvariantCulture);
        //            entry.CreationDate = TimeUtils.dateTime2javaTime(dt);
        //            modified = true;
        //        }
        //    }
        //    return modified;
        //});
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
