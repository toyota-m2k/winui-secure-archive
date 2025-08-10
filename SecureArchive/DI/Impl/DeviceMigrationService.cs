using io.github.toyota32k.toolkit.net;
using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;

namespace SecureArchive.DI.Impl;
internal class DeviceMigrationService : IDeviceMigrationService {
    private IDatabaseService _databaseService;
    private ISecureStorageService _secureStorageService;
    private readonly ILogger _logger;

    public class MigratingInfo {
        private static int _migNextId = 100;
        public string MigrationHandle = $"M{_migNextId++}";
        public OwnerInfo srcDevice { get; }
        public OwnerInfo dstDevice { get; }
        public MigratingInfo(OwnerInfo srcDevice, OwnerInfo dstDevice) {
            this.srcDevice = srcDevice;
            this.dstDevice = dstDevice;
        }
    }
    private bool _migratingWithSync = false;                // 端末間同期中フラグ
    private MigratingInfo? _migratingInfo = null;           // モバイル端末との移行情報(BeginMigration-EndMigrationの間に有効)

    public MigratingInfo? CurrentMigratingInfo => _migratingInfo;
    private bool checkMigrationHandle(string handle) {
        return _migratingInfo != null && _migratingInfo.MigrationHandle == handle;
    }

    public DeviceMigrationService(
        IDatabaseService databaseService, 
        ISecureStorageService secureStorageService,
        ILogger<DeviceMigrationService> logger
        ) {
        _logger = logger;
        _databaseService = databaseService;
        _secureStorageService = secureStorageService;
    }

    /**
     * dstDeviceId (UUID) を除くデバイス（オーナー情報）の配列を返す
     * dstDeciceId が登録済みである必要がある。（未登録なら空リストを返す）
     */
    public IList<OwnerInfo> GetDiviceList(string dstDeviceId) {
        var dstDevice = _databaseService.OwnerList.Get(dstDeviceId);
        if (dstDevice == null) {
            _logger.LogError("dstDevice is not found.");
            return new List<OwnerInfo>();
        }
        return _databaseService.OwnerList.List(o => o.OwnerId != dstDeviceId && !string.IsNullOrEmpty(o.OwnerId) && o.OwnerId!="LOCAL");
    }


    /**
     * srcDeviceId --> dstDeviceId への移行を開始する。
     * srcDeviceId, dstDeviceIdともに登録済みであることを前提とする。
     * @return migrationHandle / null: エラー（migration不可）
     */
    public (string MigrationHandle, IList<FileEntry> Targets)? BeginMigration(string srcDeviceId, string dstDeviceId) {
        lock (this) {
            _databaseService.EditEntry(entries => {
                var del = entries.Sweep();
                _logger.LogDebug($"{del} records swept.");
                return del > 0;
            });
            if (_migratingWithSync || _migratingInfo!=null) {
                _logger.LogError("sync is in progress.");
                return null;
            }
            if (string.IsNullOrEmpty(srcDeviceId) || string.IsNullOrEmpty(dstDeviceId)) {
                _logger.LogError("srcDeviceId or dstDeviceId is empty.");
                return null;
            }
            var srcDevice = _databaseService.OwnerList.Get(srcDeviceId);
            var dstDevice = _databaseService.OwnerList.Get(dstDeviceId);
            if (srcDevice == null || dstDevice == null) {
                _logger.LogError("srcDevice or dstDevice is not found.");
                return null;
            }
            var list = _databaseService.Entries.List(-1, e => e.OwnerId == srcDeviceId, resolveOwnerInfo: false);
            if(list.Count == 0) {
                _logger.LogError("No target to migrate.");
                return null;
            }

            foreach(var entry in list) {
                _logger.LogError($"Migrating: {entry.Name} - {entry.Slot}/{entry.OriginalId}");
            }

            _migratingInfo = new MigratingInfo(srcDevice, dstDevice);
            return (_migratingInfo.MigrationHandle, list);
        }
    }

    /**
     * 移行を終了する。
     */
    public bool EndMigration(string migrationHandle) {
        lock (this) {
            if (!checkMigrationHandle(migrationHandle)) {
                _logger.LogError("Invalid migration handle.");
                return true;
            }
            _migratingInfo = null;
            return false;
        }
    }

    private FileEntry? MigrateCore(IMutableTables tables, string oldOwnerId, int slot, string oldOriginalId, string newOwnerId, string newOriginalId) {
        var del = tables.Entries.GetByOriginalId(oldOwnerId, slot, oldOriginalId);
        if (del == null) {
            var entry = tables.Entries.GetByOriginalId(newOwnerId, slot, newOriginalId);
            if (entry != null) {
                _logger.LogInformation($"FileEntry({newOwnerId}/{slot}/{newOriginalId}) is already migrated.");
                // Migrateが実行される前に、同期or Backupによって、エントリーが追加されていたものと考えられる。
                // Entry Table的には何もする必要はないが、
                // 同期のたびに、これが実行されるのは無駄なので、Migration Tableに追加しておく。
                tables.DeviceMigration.Add(oldOwnerId, slot, oldOriginalId, newOwnerId, newOriginalId);
                return entry;
            }
            // エントリがバックアップされる前にMigrationの同期が実行されると、ここに入ってくる。
            // 次回の同期で、↑のif文に入って、最終的には、Migration Tableが同期される。
            _logger.LogInformation($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not found.");
            return null;
        }
        if (_migratingInfo!=null && del.OwnerId != _migratingInfo!.srcDevice.OwnerId) {
            _logger.LogError($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not owned by srcDevice.");
            return null;
        }
        tables.DeviceMigration.Add(del.OwnerId, slot, del.OriginalId, newOwnerId, newOriginalId);
        tables.Entries.Remove(del, deleteDbEntry: true);
        var newEntry = tables.Entries.Add(newOwnerId, del.Slot, del.Name, del.Size, del.Type, del.Path, del.LastModifiedDate, del.CreationDate, newOriginalId, del.Duration, del.MetaInfo, del);
        _logger.LogInformation($"Migrated: {del.Name} from {del.OwnerId}/{del.OriginalId} to {newOwnerId}/{newOriginalId}");
        return newEntry;
    }

    /**
     * HttpServerService から呼び出され、１件ずつ移行を実行する。
     * BeginMigration -> Migrate - EndMigration の順に実行する。
     * 
     * クライアントがターゲットのインポートに成功した後で、originalId, ownerId を書き換える。
     * - 移行元のFileEntryレコード(Id == srcId) を Migration Table（新規）に登録、Entries Tableからは削除する。
     * - 新しいid, originalId, ownerIdのレコードを作って追加する。
     */
    public FileEntry? Migrate(string migrationHandle, string oldOwnerId, int slot, string oldOriginalId, string newOwnerId, string newOriginalId) {
        lock (this) {
            if (!_migratingWithSync) {
                if (!checkMigrationHandle(migrationHandle)) {
                    _logger.LogError("Invalid migration handle.");
                    return null;
                }
                if (newOwnerId != _migratingInfo!.dstDevice.OwnerId) {
                    _logger.LogError("newOwnerId is not dstDeviceId.");
                    return null;
                }
                if (string.IsNullOrEmpty(newOriginalId)) {
                    _logger.LogError("newOriginalId or newOwnerId is empty.");
                    return null;
                }
            }
            FileEntry? newEntry = null;
            _databaseService.Transaction((tables) => {
                newEntry = MigrateCore(tables, oldOwnerId, slot, oldOriginalId, newOwnerId, newOriginalId);
                return newEntry != null;

                //var del = tables.Entries.GetByOriginalId(oldOwnerId, slot, oldOriginalId);
                //if (del == null) {
                //    if (tables.Entries.GetByOriginalId(newOwnerId, slot, newOriginalId) != null) {
                //        _logger.LogInformation($"FileEntry({newOwnerId}/{slot}/{newOriginalId}) is already migrated.");
                //        // Migrateが実行される前に、同期or Backupによって、エントリーが追加されていたものと考えられる。
                //        // Entry Table的には何もする必要はないが、
                //        // 同期のたびに、これが実行されるのは無駄なので、Migration Tableに追加しておく。
                //        tables.DeviceMigration.Add(oldOwnerId, slot, oldOriginalId, newOwnerId, newOriginalId);
                //        return true;
                //    }
                //    // エントリがバックアップされる前にMigrationの同期が実行されると、ここに入ってくる。
                //    // 次回の同期で、↑のif文に入って、最終的には、Migration Tableが同期される。
                //    _logger.LogInformation($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not found.");
                //    return false;
                //}
                //if (!_migratingWithSync && del.OwnerId != _migratingInfo!.srcDevice.OwnerId) {
                //    _logger.LogError($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not owned by srcDevice.");
                //    return false;
                //}
                //tables.DeviceMigration.Add(del.OwnerId, slot, del.OriginalId, newOwnerId, newOriginalId);
                //tables.Entries.Remove(del, deleteDbEntry: true);
                //newEntry = tables.Entries.Add(newOwnerId, del.Slot, del.Name, del.Size, del.Type, del.Path, del.LastModifiedDate, del.CreationDate, newOriginalId, del.Duration, del.MetaInfo, del);
                //_logger.LogInformation($"Migrated: {del.Name} from {del.OwnerId}/{del.OriginalId} to {newOwnerId}/{newOriginalId}");
                //return true;
            });
            return newEntry;
        }
    }

    public bool IsMigrated(string ownerId, int slot, string originalId) {
        lock(this) {
            return null != _databaseService.DeviceMigration.Get(ownerId, slot, originalId);
        }
    }

    /**
     * 端末間同期用メソッド（他の同期に優先して実行する）
     * Peer Server からの移行履歴を受け取り、ローカルの履歴に追加する。
     * 
     * @param history: Peer Server からの移行履歴
     * @param progress: 進捗を報告するコールバック
     * @return: ローカルにしか存在しない（==peerにputする必要がある）エントリのリスト
     */
    public IList<DeviceMigrationInfo>? ApplyHistoryFromPeerServer(IList<DeviceMigrationInfo> peerHistory, ProgressProc? progress) {
        lock (this) {
            if (_migratingWithSync || _migratingInfo != null) {
                return null;    // 二重実行は禁止
            }
            _migratingWithSync = true;
            try {
                string key(DeviceMigrationInfo info) { return $"{info.OldOwnerId}/{info.OldOriginalId}"; }
                var peerSet = new HashSet<string>(peerHistory.Select(it=>key(it))); // PeerのSet
                
                var myHistory = _databaseService.DeviceMigration.List();           // こちら側の履歴
                var mySet = new HashSet<string>(myHistory.Select(it=>key(it)));    // こちらのセット
                
                var onlyPeerList = peerHistory.Where(it => !mySet.Contains(key(it))).ToList();  // ピア側にのみ存在する履歴
                var onlyMyList = myHistory.Where(it => !peerSet.Contains(key(it))).ToList();    // こちら側にのみ存在する履歴

                _logger.LogDebug($"Sync-Migrating: only in peer = {onlyPeerList.Count} / only in mine = {onlyMyList.Count}");

                if (!onlyPeerList.IsEmpty()) {
                    _databaseService.Transaction((tables) => {
                        var count = 0;
                        var updated = false;
                        foreach (var peer in onlyPeerList) {
                            count++;
                            progress?.Invoke(count, onlyPeerList.Count);
                            _logger.LogDebug($"Sync-Migrating ({count}/{onlyPeerList.Count}): {peer.OldOriginalId}->{peer.NewOriginalId}");
                            if (MigrateCore(tables, peer.OldOwnerId, peer.Slot, peer.OldOriginalId, peer.NewOwnerId, peer.NewOriginalId) != null) {
                                updated = true;
                            }
                        }
                        return updated;
                    });
                }
                return onlyMyList;
            } finally {
                _migratingWithSync = false;
            }
        }
    }
}
