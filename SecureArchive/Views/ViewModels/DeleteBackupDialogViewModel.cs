using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels;
internal class DeleteBackupDialogViewModel {
    private IBackupService _backupService;
    private IMainThreadService _mainThreadService;
    private ILogger _logger;

    public ReactiveCommandSlim DeleteCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim CloseCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim SelectAllCommand { get; } = new ReactiveCommandSlim();

    public ObservableCollection<FileEntry> RemovedItems { get; private set; } = new ObservableCollection<FileEntry>();
    public ReactivePropertySlim<bool> Selected { get; } = new ReactivePropertySlim<bool>(false);

    public DeleteBackupDialogViewModel(IBackupService backupService, IMainThreadService mainThreadService, ILoggerFactory loggerFactory) {
        _backupService = backupService;
        _mainThreadService = mainThreadService;
        _logger = loggerFactory.CreateLogger<BackupDialogViewModel>();
        RemovedItems = new ObservableCollection<FileEntry>(_backupService.RemoteRemovedItems);
    }

    public async void Delete(IList<FileEntry> targets) {
        foreach (FileEntry target in targets) {
            if(await _backupService.DeleteBackupEntry(target)) {
                RemovedItems.Remove(target);
            }
        }
        if(RemovedItems.Count == 0) {
            CloseCommand.Execute();
        }
    }
}