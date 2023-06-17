using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design.Behavior;

namespace SecureArchive.DI.Impl;

internal struct RemoteItem {
    public string Id;
    public string Name;
    public long Size;
    public long Date;
    public string Type;
    public string Duration;
}

internal class BackupService : IBackupService {
    private readonly ILogger _logger;
    private ISecureStorageService _secureStorageService;
    private BehaviorSubject<bool> _executing = new (false);
    private BehaviorSubject<IList<RemoteItem>?> _remoteItems = new(null);
    private bool _alive = false;
    private HttpClient? _httpClient;

    private HttpClient HttpClient {
        get {
            if (_httpClient==null) { _httpClient = new HttpClient(); }
            {
                
            }
        }
    }

    public bool IsBusy {
        get { lock(this) { return _executing.Value; } }
    }

    public IObservable<bool> Executing => _executing;
    public IObservable<IList<RemoteItem>?> RemoteItems => _remoteItems;

    public BackupService(ISecureStorageService secureStorageService, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<BackupService>();
        _secureStorageService = secureStorageService;
    }

    public bool startBackup(string ownerId, string token, string url) {
        lock(this) {
            if(_executing.Value) {
                return false;
            }
            _executing.OnNext(true);
            _alive = true;
        }
        Backup(ownerId, token, url);
        return true;
    }

    private async void Backup(string ownerId, string token, string url) {
        
    }

}
