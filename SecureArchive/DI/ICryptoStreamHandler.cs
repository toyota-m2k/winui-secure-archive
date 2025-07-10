using SecureArchive.Models.DB;

namespace SecureArchive.DI;

internal interface ICryptoStreamContainer
{
    Stream Stream { get; }
    FileEntry FileEntry { get; }

}

internal interface ICryptoStreamHandler : IDisposable {
    /**
     * fileEntry のストリームをロックして返します。
     */
    ICryptoStreamContainer LockStream(FileEntry fileEntry, long id);
    /**
     * fileEntry のストリームのロックを解除します。
     */
    void UnlockStream(ICryptoStreamContainer container, long id);

    /**
     * fileEntry のストリームを破棄します。
     * - force が true の場合、InUse 状態のストリームも強制的に中止します。
     * - force が false の場合、InUse 状態のストリームは中止しません。
     * 
     * @return true: 中止成功 / false: 中止失敗（InUse状態のストリームがある）
     */
    bool AbortStream(FileEntry fileEntry, bool force);
}
