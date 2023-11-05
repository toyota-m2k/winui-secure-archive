﻿using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels {
    internal class ListPageViewModel {
        private IPageService _pageService;
        //private ICryptographyService _cryptoService;
        //private IFileStoreService _fileStoreService;
        private ISecureStorageService _secureStorageService;
        private IDatabaseService _dataBaseService;
        private ITaskQueueService _taskQueueService;
        private IStatusNotificationService _statusNotificationService;
        private IMainThreadService _mainThreadService;
        private ILogger _logger;

        public ReactiveCommandSlim AddCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim ExportCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim GoBackCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim PatchCommand { get; } = new ReactiveCommandSlim();

        public ReactivePropertySlim<ObservableCollection<FileEntry>> FileList { get; } = new (new ObservableCollection<FileEntry>());
        public IReadOnlyReactiveProperty<string> Message { get; }
        public IReadOnlyReactiveProperty<ProgressMode> ProgressMode { get; } 
        public IReadOnlyReactiveProperty<int> ProgressInPercent { get; }
        public ReadOnlyReactivePropertySlim<bool> HasMessage { get; }

        public ListPageViewModel(
            IPageService pageService, 
            //ICryptographyService cryptographyService, 
            //IFileStoreService fileStoreService, 
            ISecureStorageService secureStorageService,
            IDatabaseService dataBaseService,
            ITaskQueueService taskQueueService,
            IStatusNotificationService statusNotificationService,
            IMainThreadService mainThreadService,
            ILoggerFactory loggerFactory) {
            _pageService = pageService;
            //_cryptoService = cryptographyService;
            //_fileStoreService = fileStoreService;
            _secureStorageService = secureStorageService;
            _dataBaseService = dataBaseService;
            _taskQueueService = taskQueueService;
            _statusNotificationService = statusNotificationService;
            _mainThreadService = mainThreadService;
            _logger = loggerFactory.CreateLogger("ListPage");
            
            GoBackCommand.Subscribe(_pageService.ShowMenuPage);
            AddCommand.Subscribe(AddLocalFile);
            PatchCommand.Subscribe(() => Task.Run(()=>_secureStorageService.ConvertFastStart(_statusNotificationService)));

            FileList.Value = new ObservableCollection<FileEntry>(_dataBaseService.Entries.List(true));
            Message = _statusNotificationService.Message;
            ProgressMode = _statusNotificationService.ProgressMode;
            ProgressInPercent = _statusNotificationService.ProgressInPercent;

            HasMessage = Message.Select((it)=>!string.IsNullOrEmpty(it)).ToReadOnlyReactivePropertySlim();
            //HasMessage.Subscribe((v) => {
            //    _logger.LogDebug($"HasMessage={v}");
            //});

            //_statusNotificationService.ShowMessage("ほげほげ", 3000);

            _dataBaseService.Entries.Changes.Subscribe(change => {
                _mainThreadService.Run(() => {
                    switch (change.Type) {
                        case DataChangeInfo.Change.Add:
                            AddItem(change.Item);
                            break;
                        case DataChangeInfo.Change.Remove:
                            RemoveItem(change.Item);
                            break;
                        case DataChangeInfo.Change.Update:
                            UpdateItem(change.Item);
                            break;
                        case DataChangeInfo.Change.ResetAll:
                            FileList.Value = new ObservableCollection<FileEntry>(_dataBaseService.Entries.List(true));
                            break;
                    }
                });
            });
        }

        public async Task<bool> ExportFileTo(FileEntry entry, string outFile, ProgressProc progress) {
            try {
                await _secureStorageService.Export(entry, outFile, progress);
                return true;
            } catch (Exception ex) {
                _logger.Error(ex, "Decryption Error.");
                return false;
            }
        }

        public async Task ExportFiles(List<FileEntry> list) {
            var folder = await FolderPickerBuilder.Create(App.MainWindow)
                .SetIdentifier("SA.ExportData")
                .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                .PickAsync();
            if (folder == null) return;
            _taskQueueService.PushTask(async (mainThread) => {
                await _statusNotificationService.WithProgress("Exporting...", async (updateMessage, progress) => {
                    int success = 0;
                    int error = 0;
                    foreach (var entry in list) {
                        string outFile = Path.Combine(folder.Path, FileUtils.SafeNameOf(entry.Name));
                        if (Path.Exists(outFile)) {
                            var r = await mainThread.Run(async () => {
                                return await MessageBoxBuilder.Create(App.MainWindow)
                                    .SetMessage("The file exists. Overwrite it?")
                                    .AddButton("OK", id: true)
                                    .AddButton("Cancel", id: false)
                                    .ShowAsync();
                            });
                            if ((bool?)r != true) continue;
                        }
                        updateMessage($"Exporting {entry.Name}");
                        if(await ExportFileTo(entry, outFile, progress)) {
                            success++;
                        } else {
                            error++;
                        }
                    }
                    updateMessage($"Exported: {success}/{success+error} file(s).");
                });
            });
        }

        private void AddItem(FileEntry entry) {
            var list = FileList.Value;
            var next = list.FirstOrDefault((it) => it.LastModifiedDate > entry.LastModifiedDate);
            if(next==null) {
                list.Add(entry);
            } else {
                list.Insert(list.IndexOf(next), entry);
            }
        }

        //private void AddItems(FileEntry[] entries) {
        //    foreach (var entry in entries) {
        //          AddItem(entry);
        //    }
        //}
        private void RemoveItem(FileEntry entry) {
            var list = FileList.Value;
            var item = list.FirstOrDefault((it) => it.Id == entry.Id);
            if (item != null) {
                list.Remove(item);
            }
        }
        //private void RemoveItems(FileEntry[] entries) {
        //    foreach (var entry in entries) {
        //        RemoveItem(entry);
        //    }
        //}
        private void UpdateItem(FileEntry entry) { 
            var list = FileList.Value;
            var item = list.FirstOrDefault((it) => it.Id == entry.Id);
            if (item != null) {
                var index = list.IndexOf(item);
                if (index >= 0) {
                    list[index] = entry;
                }
            }
        }
        //private void UpdateItems(FileEntry[] entries) {
        //    foreach (var entry in entries) {
        //        UpdateItem(entry);
        //    }
        //}


        private async void AddLocalFile() {
            try {
                var list = await FileOpenPickerBuilder.Create(App.MainWindow)
                    .SetIdentifier("SA.LocalData")
                    .AddExtensionAny()
                    .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                    .PickMultiAsync();

                if (list == null) return;

                _taskQueueService.PushTask(async (mainThread) => {
                    await _statusNotificationService.WithProgress("Importing File.", async (updateMessage, progress) => {
                        //var outFolder = await _fileStoreService.GetFolder();
                        int error = 0;
                        int success = 0;
                        foreach (var item in list) {
                            updateMessage($"Importing File: {item.Name}");
                            //var fileInfo = new FileInfo(item.Path);
                            //var outFilePath = Path.Combine(outFolder!, item.Name);
                            var ext = Path.GetExtension(item.Name) ?? "*";
                            try {
                                var newEntry = await _secureStorageService.RegisterFile(item.Path, OwnerInfo.LOCAL_ID, item.Name, Guid.NewGuid().ToString("N"), null, progress);
                                if (newEntry != null) {
                                    mainThread.Run(() => {
                                        FileList.Value.Add(newEntry);
                                    });
                                    success++;
                                } else {
                                    error++;
                                }
                            }
                            catch (Exception ex) {
                                error++;
                                _logger.Error(ex, "Encryption Error.");
                            }
                        }
                        updateMessage($"Imported {success}/{success+error} file(s)");
                    });
                });

            } catch(Exception e) {
                _logger.LogError(e, "LocalFile Error");
            }
        }
    }
}
