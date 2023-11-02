using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal interface IBackupService {
    public enum Status {
        NONE,
        LISTING,
        DOWNLOADING,
    }

    /**
     * リモート（スマホ）側で追加され、SecureArchive未登録のアイテムリスト
     */
    IList<RemoteItem> RemoteNewItems { get; }
    /**
     * リモート（スマホ）側で削除され、SequreArchiveに残っているアイテムのリスト
     */
    IList<FileEntry> RemoteRemovedItems { get; }

    bool Request(string ownerId, string token, string url);
    Task<bool> DownloadTarget(RemoteItem item, ProgressProc progress, CancellationToken ct);
    Task<bool> DeleteBackupEntry(FileEntry entry);
}
