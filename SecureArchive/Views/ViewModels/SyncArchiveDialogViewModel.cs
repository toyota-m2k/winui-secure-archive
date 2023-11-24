using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels; 
internal class SyncArchiveDialogViewModel {
    ISyncArchiveService _syncArchiveService;
    IMainThreadService _mainThreadService;
    IUserSettingsService _userSettingsService;
    ILogger _logger;

    public ReactivePropertySlim<bool> Running { get; } = new(false);
    public ReactivePropertySlim<string> PeerAddress { get; } = new("");
    public ReactivePropertySlim<string> Password { get; } = new("");
    public ReactivePropertySlim<string> ErrorMessage { get; } = new("");
    public ReadOnlyReactivePropertySlim<bool> HasError { get; }
    public ReactivePropertySlim<string> ProgressMessage { get; } = new("");
    public ReadOnlyReactivePropertySlim<bool> CanStart { get; }
    public ReactivePropertySlim<long> CurrentIndex { get; } = new(0);
    public ReactivePropertySlim<long> TotalCount { get; } = new(0);
    public ReactivePropertySlim<long> CurrentBytes { get; } = new(0);
    public ReactivePropertySlim<long> TotalLength { get; } = new(0);
    public ReadOnlyReactivePropertySlim<double> CountProgress { get; }
    public ReadOnlyReactivePropertySlim<double> SizeProgress { get; }

    public ReactiveCommandSlim StartCommand { get; } = new();
    public ReactiveCommandSlim CancelCommand { get; } = new();
    public ReactiveCommandSlim CloseCommand { get; } = new();

    public SyncArchiveDialogViewModel(ISyncArchiveService syncArchiveService, IMainThreadService mainThreadService, IUserSettingsService userSettingsService, ILoggerFactory loggerFactory) { 
        _syncArchiveService = syncArchiveService;
        _mainThreadService = mainThreadService;
        _userSettingsService = userSettingsService;
        _logger = loggerFactory.CreateLogger<SyncArchiveDialogViewModel>();

        CanStart = PeerAddress.Select(it => it.IsNotEmpty()).ToReadOnlyReactivePropertySlim();
        CountProgress = CurrentIndex.CombineLatest(TotalCount, (current, total) => total > 0 ? (double)current * 100.0 / (double)total : 0).ToReadOnlyReactivePropertySlim();
        SizeProgress = CurrentBytes.CombineLatest(TotalLength, (current, total) => total > 0 ? (double)current * 100.0 / (double)total : 0).ToReadOnlyReactivePropertySlim();
        HasError = ErrorMessage.Select(it => it.IsNotEmpty()).ToReadOnlyReactivePropertySlim();
        CancelCommand.Subscribe(CancelSync);
        initPeerAddress();
    }

    private async void initPeerAddress() {
        var s = await _userSettingsService.GetAsync();
        var addr = s.PreviousPeerAddress;
        if (!string.IsNullOrEmpty(addr)) {
            PeerAddress.Value = addr;
        }
    }
    private async void updatePeerAddress() {
        var addr = PeerAddress.Value;
        if (string.IsNullOrEmpty(addr)) return;
        await _userSettingsService.EditAsync(edit => {
            if (edit.PreviousPeerAddress != addr) {
                edit.PreviousPeerAddress = addr;
                return true;
            } else { return false; }
        });
    }

    private WeakReference<XamlRoot> Parent { get; set; } = null!;

    public void AttachPage(Page page) {
        Parent = new WeakReference<XamlRoot>(page.XamlRoot);
    }

    public async void StartSync(Page page) {
        updatePeerAddress();
        if (CanStart.Value && !Running.Value) {
            ErrorMessage.Value = string.Empty;
            ProgressMessage.Value = string.Empty;
            CurrentBytes.Value = 0;
            TotalLength.Value = 0;
            CurrentIndex.Value = 0;
            TotalCount.Value = 0;
            Running.Value = true;
            var result = await _syncArchiveService.Start(PeerAddress.Value, Password.Value, page.XamlRoot, ErrorMessageProc, SyncTaskProc, CountProgressProc, SizeProgressProc);
            if (result) {
                CloseCommand.Execute();
            }
            Running.Value = false; 
        }
    }
    private void CancelSync() {
        updatePeerAddress();
        _syncArchiveService.Cancel();
        CloseCommand.Execute();
    }

    private void ErrorMessageProc(string message, bool fatal) {
        ErrorMessage.Value = message;
    }

    private void SyncTaskProc(SyncTask task) {
        _mainThreadService.Run(() => {
            switch (task) {
                case SyncTask.UploadingNew:
                    ProgressMessage.Value = "Uploading New Files.";
                    break;
                case SyncTask.UploadingUpdate:
                    ProgressMessage.Value = "Updating Peer Files.";
                    break;
                case SyncTask.DownloadNew:
                    ProgressMessage.Value = "Downloading New Files.";
                    break;
                case SyncTask.DownloadUpdate:
                    ProgressMessage.Value = "Updating My Files.";
                    break;
                case SyncTask.SyncAttributes:
                    ProgressMessage.Value = "Synchronizing Attributes.";
                    break;
                default:
                    break;
            }
        });
    }

    private void CountProgressProc(long current, long total) {
        _mainThreadService.Run(() => {
            CurrentIndex.Value = current;
            TotalCount.Value = total;
        });
    }
    private void SizeProgressProc(long current, long total) {
        _mainThreadService.Run(() => {
            CurrentBytes.Value = current;
            TotalLength.Value = total;
        });
    }




}
