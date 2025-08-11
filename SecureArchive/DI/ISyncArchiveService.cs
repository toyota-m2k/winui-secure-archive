using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI; 

public enum SyncTask {
    SyncMigrationFromPeer,
    SyncMigrationToPeer,
    UploadingNew,
    UploadingUpdate,
    DownloadNew,
    DownloadUpdate,
    Deleting,
    SyncAttributes,
}

public delegate void SyncStateProc(SyncTask syncTask);
public delegate void ErrorMessageProc(string message, bool fatal);

internal interface ISyncArchiveService {
    Task<bool> Start(string peerAddress, string peerPassword, bool peerToLocalOnly, XamlRoot? parent, ErrorMessageProc errorMessageProc, SyncStateProc syncTaskProc, ProgressProc countProgress, ProgressProc byteProgress);
    void Cancel();
}
