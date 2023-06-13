using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;

namespace SecureArchive.DI.Impl;
public class DatabaseService : IDatabaseService, IMutableTables {
    IAppConfigService _appConfigService;
    ILogger _logger;
    DBConnector _connector;

    FileEntryList _entries { get; }
    OwnerInfoList _ownerList { get; }
    KVList _kvs { get; }

    public IFileEntryList Entries => _entries;
    public IOwnerInfoList OwnerList => _ownerList;
    public IKVList KVs => _kvs;

    IMutableFileEntryList IMutableTables.Entries => _entries;
    IMutableOwnerInfoList IMutableTables.OwnerList => _ownerList;
    IMutableKVList IMutableTables.KVs => _kvs;

    public DatabaseService(IAppConfigService appConfigService, ILoggerFactory loggerFactory)
    {
        _appConfigService = appConfigService;
        _logger = loggerFactory.CreateLogger("DataService");

        _logger.LogDebug(appConfigService.DBPath);

        _connector = new DBConnector(appConfigService.DBPath);
        _entries = new FileEntryList(_connector.Entries);
        _ownerList = new OwnerInfoList(_connector.OwnerInfos);
        _kvs = new KVList(_connector.KVs);
    }

    private IMutableTables mutableTables => this;

    public bool EditEntry(Func<IMutableFileEntryList, bool> fn) {
        bool result = false;
        try {
            result = fn(mutableTables.Entries);
            return result;
        } finally {
            if (result) {
                _connector.SaveChanges();
            }
        }
    }

    public bool EditKVs(Func<IMutableKVList, bool> fn) {
        bool result = false;
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

    public bool EditOwnerList(Func<IMutableOwnerInfoList, bool> fn) {
        bool result = false;
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

    public bool Transaction(Func<IMutableTables, bool> fn) {
        bool result = false;
        using (var txn = _connector.Database.BeginTransaction()) {
            try {
                result = fn(mutableTables);
                return result;
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

    public void Update() {
        _connector.SaveChanges();
    }
}
