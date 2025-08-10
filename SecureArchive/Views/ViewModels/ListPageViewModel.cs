using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace SecureArchive.Views.ViewModels;

internal class DispOwnerInfo {
    public string? OwnerId { get; set; } = null;
    public string Name { get; set; } = string.Empty;
    public static DispOwnerInfo All = new DispOwnerInfo() {
        Name = "All"
    };
}
internal class DispSlotInfo {
    public int Slot { get; set; } = 0;
    public string Name { get; set; } = string.Empty;

    public static DispSlotInfo All = new DispSlotInfo() {
        Slot = -1,
        Name = "All"
    };
}


internal class ListPageViewModel : IListSource {
    private IPageService _pageService;
    //private ICryptographyService _cryptoService;
    //private IFileStoreService _fileStoreService;
    private ISecureStorageService _secureStorageService;
    private IDatabaseService _dataBaseService;
    private ITaskQueueService _taskQueueService;
    private IStatusNotificationService _statusNotificationService;
    private IMainThreadService _mainThreadService;
    public UtLog _logger;

    public ReactiveCommandSlim AddCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim ExportCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim GoBackCommand { get; } = new ReactiveCommandSlim();
    public ReactiveCommandSlim PatchCommand { get; } = new ReactiveCommandSlim();

    public ReactivePropertySlim<ObservableCollection<FileEntry>> FileList { get; } = new (new ObservableCollection<FileEntry>());
    public IReadOnlyReactiveProperty<string> Message { get; }
    public IReadOnlyReactiveProperty<ProgressMode> ProgressMode { get; } 
    public IReadOnlyReactiveProperty<int> ProgressInPercent { get; }
    public ReadOnlyReactivePropertySlim<bool> HasMessage { get; }

    public ReactivePropertySlim<List<DispOwnerInfo>> OwnerList { get; } = new ReactivePropertySlim<List<DispOwnerInfo>>(new List<DispOwnerInfo>());
    public ReactivePropertySlim<List<DispSlotInfo>> SlotList { get; } = new ReactivePropertySlim<List<DispSlotInfo>>(new List<DispSlotInfo>());
    public ReactivePropertySlim<DispOwnerInfo> SelectedOwner { get; } = new ReactivePropertySlim<DispOwnerInfo>(DispOwnerInfo.All, ReactivePropertyMode.DistinctUntilChanged);
    //public ReactivePropertySlim<int> SelectedOwnerIndex { get; } = new ReactivePropertySlim<int>(0, ReactivePropertyMode.DistinctUntilChanged);
    public ReactivePropertySlim<DispSlotInfo> SelectedSlot { get; } = new ReactivePropertySlim<DispSlotInfo>(DispSlotInfo.All, ReactivePropertyMode.DistinctUntilChanged);

    public IList<FileEntry> GetFileList() {
        return FileList.Value;
    }

    public ListPageViewModel(
        IPageService pageService, 
        //ICryptographyService cryptographyService, 
        //IFileStoreService fileStoreService, 
        ISecureStorageService secureStorageService,
        IDatabaseService dataBaseService,
        ITaskQueueService taskQueueService,
        IStatusNotificationService statusNotificationService,
        IMainThreadService mainThreadService) {
        _pageService = pageService;
        //_cryptoService = cryptographyService;
        //_fileStoreService = fileStoreService;
        _secureStorageService = secureStorageService;
        _dataBaseService = dataBaseService;
        _taskQueueService = taskQueueService;
        _statusNotificationService = statusNotificationService;
        _mainThreadService = mainThreadService;
        _logger = UtLog.Instance(typeof(ListPageViewModel));

        GoBackCommand.Subscribe(_pageService.ShowMenuPage);
        AddCommand.Subscribe(AddLocalFile);

        FileList.Value = new ObservableCollection<FileEntry>();
        Task.Run(() => {
            UpdateOwnerList();
            UpdateSlotList();
            ResetAllItems();
        });
        Message = _statusNotificationService.Message;
        ProgressMode = _statusNotificationService.ProgressMode;
        ProgressInPercent = _statusNotificationService.ProgressInPercent;

        HasMessage = Message.Select((it) => !string.IsNullOrEmpty(it)).ToReadOnlyReactivePropertySlim();

        SelectedSlot.Subscribe((it) => {
            ResetAllItems();
        });
        SelectedOwner.Subscribe((it) => {
            ResetAllItems();
        });

        _dataBaseService.Entries.Changes.Subscribe(change => {
            Task.Run(() => {
                var uo = UpdateOwnerList();
                var us = UpdateSlotList();
                if (uo || us) {
                    return;
                }
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
                            ResetAllItems();
                            break;
                    }
                });
            });
        });
    }

    class OwnerComparer : IEqualityComparer<DispOwnerInfo> {
        public bool Equals(DispOwnerInfo? x, DispOwnerInfo? y) {
            if (x == null && y == null) return true;
            if (x == null) return false;
            if (y == null) return false;
            return x.OwnerId == y.OwnerId;
        }

        public int GetHashCode([DisallowNull] DispOwnerInfo obj) {
            return obj.OwnerId?.GetHashCode() ?? "null".GetHashCode();
        }
    }
    static OwnerComparer _ownerComparer = new OwnerComparer();

    private bool UpdateOwnerList() {
        var availableOwners = _dataBaseService.Entries.AvailableOwnerIds();
        var owners = 
            new List<DispOwnerInfo>() { DispOwnerInfo.All }
            .Concat(
            _dataBaseService.OwnerList
            .List()
            .Where(it=>availableOwners
            .Contains(it.OwnerId))
            .Select((it) => new DispOwnerInfo() { OwnerId = it.OwnerId, Name = it.Name })
            ).ToList();

        if (OwnerList.Value.SequenceEqual(owners, _ownerComparer)) {
            return false;
        }
        var selectionChanging = SelectedOwner.Value.OwnerId != DispOwnerInfo.All.OwnerId && owners.Find(it => it.OwnerId == SelectedOwner.Value.OwnerId) == null;
        _mainThreadService.Run(() => {
            OwnerList.Value = owners;
            if (selectionChanging) {
                SelectedOwner.Value = owners[0];
            }
        });
        return selectionChanging;
    }

    private class SlotComparer : IEqualityComparer<DispSlotInfo> {
        public bool Equals(DispSlotInfo? x, DispSlotInfo? y) {
            if (x == null && y == null) return true;
            if (x == null) return false;
            if (y == null) return false;
            return x.Slot == y.Slot;
        }

        public int GetHashCode([DisallowNull] DispSlotInfo obj) {
            return obj.Slot.GetHashCode();
        }
    }
    private static SlotComparer _slotComparer = new SlotComparer();

    private bool UpdateSlotList() {
        var slots =
            new List<DispSlotInfo>() { DispSlotInfo.All }
            .Concat(_dataBaseService.Entries.AvailableSlots()
                .Select(it => new DispSlotInfo() { Slot = it, Name = it == 0 ? "Default" : $"Slot-{it}" }))
            .ToList();
        if (SlotList.Value.SequenceEqual(slots, _slotComparer)) {
            return false;
        }
        var selectionChanging = SelectedSlot.Value.Slot != DispSlotInfo.All.Slot && slots.Find(it => it.Slot == SelectedSlot.Value.Slot) == null;
        _mainThreadService.Run(() => {
            SlotList.Value = slots;
            if (selectionChanging) {
                SelectedSlot.Value = slots[0];
            }
        });
        return selectionChanging;
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
        //var slots = _dataBaseService.Entries.AvailableSlots();
        //foreach (var slot in slots) {
        //    var xxx = _dataBaseService.Entries.List(slot, true);
        //    foreach (var x in xxx) {
        //        if (!File.Exists(x.Path)) {
        //            _logger.Error($"No Data: {x.Id} {x.Deleted} {x.Name}");
        //        }
        //    }
        //}


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

    public void Sweep() {
        Task.Run(() => {
            var (entries, migrations) = _dataBaseService.Sweep();
            _mainThreadService.Run(async () => {
                await MessageBoxBuilder.Create(App.MainWindow)
                    .SetTitle("Swept")
                    .SetMessage($"{entries} file entries removed.\r\n{migrations} migration records removed.")
                    .AddButton("OK")
                    .ShowAsync();
            });
        });
    }

    public async Task<bool> ConvertFastStart(List<FileEntry> list) {
        try {
            await _secureStorageService.ConvertFastStart(_statusNotificationService, list);
            return true;
        }
        catch (Exception ex) {
            _logger.Error(ex, "FastStart Error.");
            return false;
        }
    }

    private void AddItem(FileEntry entry) {
        if (SelectedSlot.Value.Slot != -1 && SelectedSlot.Value.Slot != entry.Slot) return; // Skip if not in the selected slot
        if (SelectedOwner.Value.OwnerId != null && SelectedOwner.Value.OwnerId != entry.OwnerId) return; // Skip if not in the selected owner

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

    private void ResetAllItems() {
        var ownerId = SelectedOwner.Value.OwnerId;
        var slot = SelectedSlot.Value.Slot;
        if (ownerId == null) {
            FileList.Value = new ObservableCollection<FileEntry>(_dataBaseService.Entries.List(slot, true));
        }
        else {
            FileList.Value = new ObservableCollection<FileEntry>(_dataBaseService.Entries.List(slot, it => it.OwnerId == ownerId, true));
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
                            var newEntry = await _secureStorageService.RegisterFile(item.Path, OwnerInfo.LOCAL_ID, 0, item.Name, Guid.NewGuid().ToString("N"), 0L, null, progress);
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
            _logger.Error(e, "LocalFile Error");
        }
    }

    private string prevSortKey = string.Empty;
    private bool ascending = false;

    public bool SortBy(string key) {
        string prevKey = prevSortKey.IsEmpty() ? (key == "Name" ? "Id" : key) : prevSortKey;
        if (prevSortKey == key) {
            ascending = !ascending;
        } else { 
            prevSortKey = key;
        }

        var list = FileList.Value;
        var sorted = ascending
            ? list.OrderBy((it) => it.GetType().GetProperty(key)?.GetValue(it)).ThenBy((it) => it.GetType().GetProperty(prevKey)?.GetValue(it))
            : list.OrderByDescending((it) => it.GetType().GetProperty(key)?.GetValue(it)).ThenBy((it) => it.GetType().GetProperty(prevKey)?.GetValue(it));
        FileList.Value = new ObservableCollection<FileEntry>(sorted);
        return ascending;
    }

    private bool Validate(bool repair) {
        long getLengthByRead(Stream stream) {
            var buffer = new byte[1024];
            long length = 0;
            while (true) {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) {
                    return length;
                }
                length += read;
            }
        }

        Stream? openEntryStream(FileEntry entry) {
            try {
                return _secureStorageService.OpenEntry(entry);
            } catch (FileNotFoundException) {
                _logger.Error($"Cannot open entry: {entry.Name}[ID={entry.Id}]");
                if(repair) {
                    _dataBaseService.EditEntry((entries) => {
                        entries.Remove(entry);
                        return true;
                    });
                }
            }
            catch (Exception e) {
                _logger.Error(e, $"Error: {entry.Name}[ID={entry.Id}] {e.Message} ");
            }
            return null;
        }


        var list = FileList.Value.ToArray();
        var result = true;
        var count = list.Length;
        int i = 0;
        foreach (var entry in list) {
            i++;
            if (entry.IsDeleted) continue;
            _logger.Debug($"Validating ({i}/{count}): {entry.Name}[ID={entry.Id}]");
            var stream = openEntryStream(entry);
            if (stream != null) {
                using (stream) {
                    if (stream == null) {
                        _logger.Error($"Cannot open entry: {entry.Name}[ID={entry.Id}]");
                    }
                    else {
                        long length = getLengthByRead(stream);
                        if (length != entry.Size) {
                            _logger.Error($"Size mismatch: {entry.Name}[ID={entry.Id}] DB={entry.Size}, Stream={length}");
                            if (repair) {
                                _dataBaseService.EditEntry((entries) => {
                                    entries.Update(
                                        entry.OwnerId,
                                        entry.Slot,
                                        entry.Name,
                                        length,
                                        entry.Type,
                                        entry.Path,
                                        entry.LastModifiedDate,
                                        entry.OriginalId,
                                        entry.Duration,
                                        entry.MetaInfo,
                                        new ItemExtAttributes(entry.AttrDataDic));
                                    return true;
                                });
                            }
                            result = false;
                        }
                    }
                }
            }
        }
        _logger.Info($"Validation Completed: {result}");
        return result;
    }
}
