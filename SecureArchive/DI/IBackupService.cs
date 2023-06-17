using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal interface IBackupService {
    bool IsBusy { get; }
    IObservable<bool> Executing { get; }
    bool Backup(string ownerId, string token, string url);
    void Cancel();
}
