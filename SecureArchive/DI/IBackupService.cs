using SecureArchive.DI.Impl;
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

    IList<RemoteItem> RemoteItems { get; }

    bool Request(string ownerId, string token, string url);
    Task<bool> DownloadTarget(RemoteItem item, ProgressProc progress, CancellationToken ct);
}
