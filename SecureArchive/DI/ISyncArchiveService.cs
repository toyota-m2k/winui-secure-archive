using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI; 

public enum SyncTask {
    UploadingNew,
    UploadingUpdate,
    DownloadNew,
    DownloadUpdate,
}

public delegate void SyncStateProc(SyncTask syncTask);
public delegate void ErrorMessageProc(string message, bool fatal);

internal interface ISyncArchiveService {
    Task<bool> Start(string peerAddress, string peerPassword, XamlRoot? parent, ErrorMessageProc errorMessageProc, SyncStateProc syncTaskProc, ProgressProc countProgress, ProgressProc byteProgress);
    void Cancel();
}
