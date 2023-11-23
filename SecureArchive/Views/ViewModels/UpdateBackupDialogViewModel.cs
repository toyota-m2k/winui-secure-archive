using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels;

internal class UpdateBackupDialogViewModel {
    private IBackupService _backupService;
    private IMainThreadService _mainThreadService;
    private CancellationTokenSource? _cts = null;
    public ReactivePropertySlim<int> Progress { get; } = new ReactivePropertySlim<int>(0);
    public ReactiveCommandSlim CancelCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim<bool> CompleteCommand { get; } = new ReactiveCommandSlim<bool>();

    public UpdateBackupDialogViewModel(IBackupService backupService, IMainThreadService mainThreadService) {
        _backupService = backupService;
        _mainThreadService = mainThreadService;
        CancelCommand.Subscribe(Cancel);
    }

    public void ExecuteUpdate() {
        Task.Run(async () => {
            _cts = new CancellationTokenSource();
            int total = _backupService.RemoteModifiedItems.Count;
            int count = 0;
            foreach (var item in _backupService.RemoteModifiedItems) {
                count++;
                _mainThreadService.Run(() => {
                    Progress.Value = (int)((float)count / total * 100);
                });
                await _backupService.UpdateBackupEntry(item, _cts.Token);
            }
            _mainThreadService.Run(() => {
                CompleteCommand.Execute(true);
            });
        });
    }

    private void Cancel() {
        _cts?.Cancel();
        CompleteCommand.Execute(false);
    }
}
