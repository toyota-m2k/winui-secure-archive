using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Utils;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace SecureArchive.Views.ViewModels;

internal class BackupDialogViewModel {
    private IBackupService _backupService;
    private IMainThreadService _mainThreadService;
    private ILogger _logger;
    
    public ReactiveCommandSlim StartCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim StopCommand { get; } = new ReactiveCommandSlim();

    public ReactivePropertySlim<bool> Downloading { get; } = new ReactivePropertySlim<bool>(false);
    public ReactivePropertySlim<bool> Selected { get; } = new ReactivePropertySlim<bool>(false); 

    public ReactivePropertySlim<string> CurrentItem { get; } = new ReactivePropertySlim<string>("");
    public ReactivePropertySlim<long> CurrentBytes { get; } = new ReactivePropertySlim<long>(0);
    public ReactivePropertySlim<long> TotalBytes { get; } = new ReactivePropertySlim<long>(0);
    public ReactivePropertySlim<long> CurrentIndex { get; } = new ReactivePropertySlim<long>(0);
    public ReactivePropertySlim<long> TotalCount { get; } = new ReactivePropertySlim<long>(0);

    public ReadOnlyReactivePropertySlim<double> CountProgress{ get; }
    public ReadOnlyReactivePropertySlim<double> SizeProgress { get; }

    public ObservableCollection<RemoteItem> RemoteItems { get; private set; } = new ObservableCollection<RemoteItem>();
    public BackupDialogViewModel(IBackupService backupService, IMainThreadService mainThreadService,ILoggerFactory loggerFactory) {
        _backupService = backupService;
        _mainThreadService = mainThreadService;
        _logger = loggerFactory.CreateLogger<BackupDialogViewModel>();
        RemoteItems = new ObservableCollection<RemoteItem>(_backupService.RemoteItems);
        CountProgress = CurrentIndex.CombineLatest(TotalCount, (current, total) => total>0 ? (double)current*100.0 / (double)total: 0).ToReadOnlyReactivePropertySlim();
        SizeProgress = CurrentBytes.CombineLatest(TotalBytes, (current, total) => total>0 ? (double)current*100.0 / (double)total : 0).ToReadOnlyReactivePropertySlim();
        StopCommand.Subscribe(Stop);
    }



    private void Progress(long current, long total) {
        _mainThreadService.Run(() => {
            CurrentBytes.Value = current;
            TotalBytes.Value = total;
        });
    }

    private CancellationTokenSource? _cts = null;

    public void Download(IList<RemoteItem> targets) {
        if(Downloading.Value) {
            return;
        }
        _cts = new CancellationTokenSource();
        Downloading.Value = true;

        TotalCount.Value = targets.Count;
        CurrentIndex.Value = 0;
        CurrentBytes.Value = 0;
        TotalBytes.Value = 0;
        CurrentItem.Value = "";

        Task.Run(async () => {
            try {
                foreach(var item in targets) {
                    _mainThreadService.Run(() => {
                        CurrentIndex.Value++;
                        CurrentBytes.Value = 0;
                        TotalBytes.Value = 0;
                        CurrentItem.Value = item.Name;
                    });
                    if(await _backupService.DownloadTarget(item, Progress, _cts.Token)) {
                        _mainThreadService.Run(() => {
                            RemoteItems.Remove(item);
                        });
                    }
                }
            } catch(Exception e) {
                _logger.Error(e);
            } finally {
                _mainThreadService.Run(() => {
                    Downloading.Value = false;
                });
                _cts = null;
            }
        });
    }

    public void Stop() {
        if(_cts != null) {
            _cts.Cancel();
            _cts = null;
        }
    }
}
