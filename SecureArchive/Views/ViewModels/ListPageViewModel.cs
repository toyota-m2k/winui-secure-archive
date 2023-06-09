using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
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
        private ICryptographyService _cryptoService;
        private IFileStoreService _fileStoreService;
        private IDataService _dataService;
        private ITaskQueueService _taskQueueService;
        private IStatusNotificationService _statusNotificationService;
        private ILogger _logger;

        public ReactiveCommandSlim AddCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim ExportCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim GoBackCommand { get; } = new ReactiveCommandSlim();

        public ReactivePropertySlim<ObservableCollection<FileEntry>> FileList { get; } = new (new ObservableCollection<FileEntry>());
        public IReadOnlyReactiveProperty<string> Message { get; }
        public IReadOnlyReactiveProperty<ProgressMode> ProgressMode { get; } 
        public IReadOnlyReactiveProperty<int> ProgressInPercent { get; }
        public ReadOnlyReactivePropertySlim<bool> HasMessage { get; }

        public ListPageViewModel(
            IPageService pageService, 
            ICryptographyService cryptographyService, 
            IFileStoreService fileStoreService, 
            IDataService dataService,
            ITaskQueueService taskQueueService,
            IStatusNotificationService statusNotificationService,
            ILoggerFactory loggerFactory) {
            _pageService = pageService;
            _cryptoService = cryptographyService;
            _fileStoreService = fileStoreService;
            _dataService = dataService;
            _taskQueueService = taskQueueService;
            _statusNotificationService = statusNotificationService;
            _logger = loggerFactory.CreateLogger("ListPage");
            
            GoBackCommand.Subscribe(_pageService.ShowMenuPage);
            AddCommand.Subscribe(AddLocalFile);

            FileList.Value = new ObservableCollection<FileEntry>(_dataService.Entries.List());
            Message = _statusNotificationService.Message;
            ProgressMode = _statusNotificationService.ProgressMode;
            ProgressInPercent = _statusNotificationService.ProgressInPercent;

            HasMessage = Message.Select((it)=>!string.IsNullOrEmpty(it)).ToReadOnlyReactivePropertySlim();
            //HasMessage.Subscribe((v) => {
            //    _logger.LogDebug($"HasMessage={v}");
            //});

            //_statusNotificationService.ShowMessage("ほげほげ", 3000);
        }

        public void FixedTermMessage(string message, long timeMs) {

        }

        public async Task<bool> ExportFileTo(FileEntry entry, string targetFolderPath, bool allowOverwite) {
            string outFile = Path.Combine(targetFolderPath, entry.Name);
            if(Path.Exists(outFile) && !allowOverwite) {
                var r = await MessageBoxBuilder.Create(App.MainWindow)
                    .SetMessage("The file exists. Overwrite it?")
                    .AddButton("OK", id: true)
                    .AddButton("Cancel", id: false)
                    .ShowAsync();
                if((bool?)r !=true) { return false; }
            }
            try {
                using (var inStream = File.OpenRead(entry.Path))
                using (var outStream = File.OpenWrite(outFile)) {
                    await _cryptoService.DecryptStreamAsync(inStream, outStream, (current, total) => {
                        _logger.LogDebug("Decrypting {0} ... {1}/{2}", entry.Name, current, total);
                    });
                }
                return true;
            } catch (Exception ex) {
                _logger.LogError(ex, "Decryption Error.");
                return false;
            }
        }

        public async Task ExportFiles(IEnumerable<FileEntry> list) {
            var folder = await FolderPickerBuilder.Create(App.MainWindow)
                .SetIdentifier("SA.ExportData")
                .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                .PickAsync();
            if (folder == null) return;
            foreach (var entry in list) {
                await ExportFileTo(entry, folder.Path, false);
            }
        }

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
                        var outFolder = await _fileStoreService.GetFolder();
                        int error = 0;
                        int success = 0;
                        foreach (var item in list) {
                            updateMessage($"Importing File: {item.Name}");
                            var fileInfo = new FileInfo(item.Path);
                            var outFilePath = Path.Combine(outFolder!, item.Name);
                            var ext = Path.GetExtension(item.Name) ?? "*";
                            try {
                                using (var inStream = File.OpenRead(item.Path))
                                using (var outStream = File.OpenWrite(outFilePath)) {
                                    //Debug.Assert(length == fileInfo.Length);
                                    await _cryptoService.EncryptStreamAsync(inStream, outStream, progress);
                                }
                                _dataService.EditEntry((entry) => {
                                    var newEntry = entry.Add("@Local", item.Name, fileInfo.Length, ext, outFilePath, fileInfo.LastWriteTimeUtc.Ticks);
                                    mainThread.Run(() => {
                                        FileList.Value.Add(newEntry);
                                    });
                                    return true;
                                });
                                success++;
                            }
                            catch (Exception ex) {
                                error++;
                                _logger.LogError(ex, "Encryption Error.");
                                if (File.Exists(outFilePath)) {
                                    FileUtils.SafeDelete(outFilePath);
                                }
                            }
                        }
                        updateMessage($"Imported {success}/{success+error} file(s)");
                    });
                });

            } catch(Exception e) {
                _logger.LogError(e, "AddLocalFile Error");
            }
        }
    }
}
