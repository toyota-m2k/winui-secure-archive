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
    private bool _migratingWithSync = false;
    private MigratingInfo? _migratingInfo = null;
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
            if (_migratingWithSync) {
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

    /**
     * クライアントがターゲットのインポートに成功した後で、originalId, ownerId を書き換える。
     * - 移行元のFileEntryレコード(Id == srcId) を Migration Table（新規）に登録、Entries Tableからは削除する。
     * - 新しいid, originalId, ownerIdのレコードを作って追加する。
     * 
     * 端末間同期時は、Migration Table の同期を最優先で実行すること。
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
                var del = tables.Entries.GetByOriginalId(oldOwnerId, slot, oldOriginalId);
                if (del == null) {
                    if (tables.Entries.GetByOriginalId(newOwnerId, slot, newOriginalId) != null) {
                        _logger.LogInformation($"FileEntry({newOwnerId}/{slot}/{newOriginalId}) is already migrated.");
                        // Migrateが実行される前に、同期or Backupによって、エントリーが追加されていたものと考えられる。
                        // Entry Table的には何もする必要はないが、
                        // 同期のたびに、これが実行されるのは無駄なので、Migration Tableに追加しておく。
                        tables.DeviceMigration.Add(oldOwnerId, slot, oldOriginalId, newOwnerId, newOriginalId);
                        return true;
                    }
                    // エントリがバックアップされる前にMigrationの同期が実行されると、ここに入ってくる。
                    // 次回の同期で、↑のif文に入って、最終的には、Migration Tableが同期される。
                    _logger.LogInformation($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not found.");
                    return false;
                }
                if (!_migratingWithSync && del.OwnerId != _migratingInfo!.srcDevice.OwnerId) {
                    _logger.LogError($"FileEntry({oldOwnerId}/{slot}/{oldOriginalId}) is not owned by srcDevice.");
                    return false;
                }
                tables.DeviceMigration.Add(del.OwnerId, slot, del.OriginalId, newOwnerId, newOriginalId);
                tables.Entries.Remove(del, deleteDbEntry: true);
                newEntry = tables.Entries.Add(newOwnerId, del.Slot, del.Name, del.Size, del.Type, del.Path, del.LastModifiedDate, del.CreationDate, newOriginalId, del.Duration, del.MetaInfo, del);
                _logger.LogInformation($"Migrated: {del.Name} from {del.OwnerId}/{del.OriginalId} to {newOwnerId}/{newOriginalId}");
                return true;
            });
            return newEntry;
        }
    }

    public bool IsMigrated(string ownerId, int slot, string originalId) {
        lock(this) {
            return null != _databaseService.DeviceMigration.Get(ownerId, slot, originalId);
        }
    }

    public IList<DeviceMigrationInfo> ApplyHistoryFromPeerServer(IList<DeviceMigrationInfo> history) {
        lock (this) {
            _migratingWithSync = true;
            try {
                var common = new HashSet<string>();
                foreach (var peer in history) {
                    var mine = _databaseService.DeviceMigration.Get(peer.OldOwnerId, peer.Slot, peer.OldOriginalId);
                    if ( mine == null) {
                        // peerにのみ存在する
                        Migrate("sync", peer.OldOwnerId, peer.Slot, peer.OldOriginalId, peer.NewOwnerId, peer.NewOriginalId);
                    } else {
                        common.Add(mine.OldOwnerId + "/" + mine.OldOriginalId);
                    }
                }
                return _databaseService.DeviceMigration.List().Where(x => !common.Contains(x.OldOwnerId + "/" + x.OldOriginalId)).ToList();
            } finally {
                _migratingWithSync = false;
            }
        }
    }
}
