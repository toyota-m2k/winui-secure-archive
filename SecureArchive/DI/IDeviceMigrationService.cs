using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI; 
internal interface IDeviceMigrationService {
    public interface IMigrationHandle {
        string MigrationID { get; }
    }
    /**
     * dstDeviceId (UUID) を除くデバイス（オーナー情報）の配列を返す
     * dstDeciceId が登録済みである必要がある。（未登録なら空リストを返す）
     */
    IList<OwnerInfo> GetDiviceList(string dstDeviceId);

    /**
     * srcDeviceId --> dstDeviceId への移行を開始する。
     * 実行中のMigrationがある場合は、それをキャンセルして新しいMigrationを開始する。
     * 
     * @return migrationHandle / null: エラー（migration不可）
     */
    (string MigrationHandle, IList<FileEntry> Targets)? BeginMigration(string srcDeviceId, string dstDeviceId);
    /**
     * 移行を終了する。
     */
    bool EndMigration(string migrationHandle);

    /**
     * クライアントがターゲットのインポートに成功した後で、originalId, ownerId を書き換える。
     * - 移行元のFileEntryレコード(Id == srcId) を Migration Table（新規）に登録、Entries Tableからは削除する。
     * - 新しいid, originalId, ownerIdのレコードを作って追加する。
     * 
     * 端末間同期時は、Migration Table の同期を最優先で実行すること。
     */
    FileEntry? Migrate(string migrationHandle, string oldOwnerId, string oldOriginalId, string newOwnerId, string newOriginalId);

    bool IsMigrated(string ownerId, string originalId);


    IList<DeviceMigrationInfo> ApplyHistoryFromPeerServer(IList<DeviceMigrationInfo> history);
}
