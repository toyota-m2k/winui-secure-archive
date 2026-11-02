using io.github.toyota32k.toolkit.net;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;

namespace SecureArchive.Views.ViewModels;

internal class DispOwnerInfo {
    public string OwnerId { get; private set; }
    public string Name { get; private set; }
    public DispOwnerInfo(string ownerId, string name) {
        OwnerId = ownerId;
        Name = name;
    }
    public static DispOwnerInfo All = new DispOwnerInfo("$$ALL", "All");
    
    public static DispOwnerInfo From(OwnerInfo oi) {
        return new DispOwnerInfo(oi.OwnerId, oi.Name);
    }


    public override bool Equals(object? obj) {
        if (obj is DispOwnerInfo other) {
            return OwnerId == other.OwnerId;
        }
        return false;
    }

    public override int GetHashCode() {
        return OwnerId.GetHashCode();
    }
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

    public class FileEntryItem {
        public FileEntry Entry { get;}
        public long Id { get; }
        public string OwnerId { get; }
        public string OriginalId { get; }
        public string Name { get; }
        public long Size { get; }
        public string Type { get; }
        public string? MetaInfo { get; }
        public string Path { get; }
        public long RegisteredDate { get; }
        public long CreationDate { get; }
        public long LastModifiedDate { get; }
        public long Deleted { get; }
        public long ExtAttrDate { get; }
        // 以下、extended attributes
        public int Rating { get; }
        public int Mark { get; }
        public string? Label { get; }
        public string? Category { get; }
        public string? Chapters { get; }
        public long Duration { get; } = 0;
        public int Slot { get; } = 0;
        public OwnerInfo? OwnerInfo { get; } = OwnerInfo.Empty;

        public bool IsDeleted => Deleted != 0;

        private FileEntryItem(FileEntry entry) {
            Entry = entry;
            Id = entry.Id;
            OwnerId = entry.OwnerId;
            OriginalId = entry.OriginalId;
            Name = entry.Name;
            Size = entry.Size;
            Type = entry.Type;
            MetaInfo = entry.MetaInfo;
            Path = entry.Path;
            RegisteredDate = entry.RegisteredDate;
            CreationDate = entry.CreationDate;
            LastModifiedDate = entry.LastModifiedDate;
            Deleted = entry.Deleted;
            ExtAttrDate = entry.ExtAttrDate;
            Rating = entry.Rating;
            Mark = entry.Mark;
            Label = entry.Label;
            Category = entry.Category;
            Chapters = entry.Chapters;
            Duration = entry.Duration;
            Slot = entry.Slot;
            OwnerInfo = entry.OwnerInfo;
        }

        public static FileEntryItem FromEntry(FileEntry entry) {
            return new FileEntryItem(entry);
        }
    }

    private FileEntryItem FromEntry(FileEntry entry) {
        return FileEntryItem.FromEntry(_dataBaseService.Entries.ResolveOwnerInfo(entry));
    }

    public ReactivePropertySlim<ObservableCollection<FileEntryItem>> FileList { get; } = new (new ObservableCollection<FileEntryItem>());
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
        return FileList.Value.Select(it=>it.Entry).ToList();
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

        FileList.Value = new ObservableCollection<FileEntryItem>();
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
                    // 全リスト更新済みなので、個別の変更は不要
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
                .Where(it=> availableOwners.Contains(it.OwnerId))
                .Select(it => DispOwnerInfo.From(it))
            ).ToList();
        Debug.Assert(owners[0] == DispOwnerInfo.All);
        if (OwnerList.Value.SequenceEqual(owners, _ownerComparer)) {
            // OwnerListが変化していなければ何もしない
            return false;
        }
        var newOwner = owners.Find(it => it == SelectedOwner.Value) ?? DispOwnerInfo.All;
        _mainThreadService.Run(() => {
            OwnerList.Value = owners;
            SelectedOwner.Value = newOwner;
        });
       return true;
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

    //private static Regex regDate = new Regex(@"(?<date>\d{4}\.\d{2}\.\d{2}-\d{2}:\d{2}:\d{2})");

    //private static string Filename2DateString(string filename) {
    //    var match = regDate.Match(filename);
    //    if (match.Success) {
    //        return match.Groups["date"].Value;
    //    }
    //    throw new ArgumentException("Filename does not contain a valid date string in the format 'yyyy.MM.dd-HH:mm:ss'.", nameof(filename));
    //}

    //private static long DateStringToUnixTime(string dateString) {
    //    var format = "yyyy.MM.dd-HH:mm:ss";
    //    var dt = DateTime.ParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    //    var unixTime = new DateTimeOffset(dt).ToUnixTimeSeconds();
    //    return unixTime;
    //}

    //private static long Filename2UnixTime(string filename) {
    //    try {
    //        var dateString = Filename2DateString(filename);
    //        return DateStringToUnixTime(dateString);
    //    } catch(Exception e) {
    //        return -1;
    //    }
    //}

    /**
     * CreationDate が不正なエントリが見つかったので、是正するためのメンテナンスメソッド
     */
    private void PatchCreationDate() {
        _dataBaseService.EditEntry(entries => {
            var count = 0;
            foreach (var entry in entries.List()) {
                var creationDateString = FileEntry.Filename2DateString(entry.Name);
                var creationDateFromFilename = FileEntry.Filename2UnixTime(creationDateString);
                var dbDateRaw = TimeUtils.javaTime2dateTime(entry.CreationDate).ToString("yyyy.MM.dd-HH:mm:ss");
                var dbDateUtc= TimeUtils.javaTime2dateTime(entry.CreationDate).ToUniversalTime().ToString("yyyy.MM.dd-HH:mm:ss");
                var dbDateLocal = TimeUtils.javaTime2dateTime(entry.CreationDate).ToLocalTime().ToString("yyyy.MM.dd-HH:mm:ss");
                if (creationDateString == dbDateRaw) {
                    Debug.WriteLine($"DB:{dbDateRaw} -- Raw");
                }
                else if (creationDateString == dbDateUtc) {
                    Debug.WriteLine($"DB:{dbDateUtc} -- UTC");
                } 
                else if (creationDateString == dbDateLocal) {
                    Debug.WriteLine($"DB:{dbDateLocal} -- Local");
                    entry.CreationDate = creationDateFromFilename;
                    count++;
                }
                else /*if (creationDateString != dbDateUtc && creationDateString != dbDateLocal)*/ {
                    Debug.WriteLine($"DB: {dbDateLocal} - File:{creationDateString}");
                    entry.CreationDate = creationDateFromFilename;
                    count++;
                }
            }
            if (count > 0) {
                _logger.Info($"Patching CreationDate: {count} entries updated.");
            }
            else {
                _logger.Info("No entries to patch.");
            }
            return count > 0;
        });
    }

    // DBに登録されたファイルサイズが不正なエントリを修正するメンテナンスメソッド
    public void RepairFileSize() {
        (FileEntry entry, long length) getActuelEntryLength(FileEntry entry) {
            try {
                var inner = _secureStorageService.OpenEntry(entry);
                if (inner == null) {
                    _logger.Error($"cannot open {entry.Name}: {entry.Path}");
                    return (entry, -1);
                }
                var stream = new SeekableInputStream(inner, current => {
                    current?.Dispose();
                    return _secureStorageService.OpenEntry(entry);
                });
                using (stream) {
                    var length = stream.ReadLength();
                    if (entry.Size != length) {
                        _logger.Warn($"size mismatch -- recorded size: {entry.Size} / actual size: {length}");
                    } else {
                        Logger.debug($"ok: {entry.Name}");
                    }
                    return (entry, length);
                }
            } catch (Exception e) {
                _logger.Error(e, $"cannot open { entry.Name}: { entry.Path}");
                return (entry, -1);
            }
        }

        Task.Run(() => {
            var list = _dataBaseService.Entries.List().Where(it=>it.Size==0).Select(entry => {
                return getActuelEntryLength(entry);
            }).Where(it => {
                return it.entry.Size != it.length;
            }).ToList();
            if (list.Count > 0) {
                _dataBaseService.EditEntry(table => {
                    var modified = false;
                    foreach (var it in list) {
                        if (it.length < 0) {
                            // need to swipe?
                            _logger.Error($"cannot get actual size for entry: {it.entry.Name}");
                            continue;
                        }
                        table.Update(it.entry, size: it.length);
                        modified = true;
                    }
                    return modified;
                });
            }

            //_mainThreadService.Run(() => {
            //    var list = FileList.Value.ToList();
            //    FileList.Value.Clear();
            //    foreach(var item in list) {
            //        _logger.Debug($"{item.Name} : Size={item.Size}");
            //        FileList.Value.Add(item);
            //    }
            //});
        });
    }

    // 実ファイルが存在しないエントリをDBから削除するメンテナンスメソッド
    public void Sweep() {
        Task.Run(() => {
            //PatchCreationDate();
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
        if (SelectedOwner.Value != DispOwnerInfo.All && SelectedOwner.Value.OwnerId != entry.OwnerId) return; // Skip if not in the selected owner

        var list = FileList.Value;
        var next = list.FirstOrDefault((it) => it.LastModifiedDate > entry.LastModifiedDate);
        if(next==null) {
            list.Add(FromEntry(entry));
        } else {
            list.Insert(list.IndexOf(next), FromEntry(entry));
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
                //list[index] = FileEntryItem.FromFileEntry(entry);
                list.RemoveAt(index);
                list.Insert(index, FromEntry(entry));
            }
        }
    }

    private void ResetAllItems() {
        if (SelectedOwner.Value == null) return;
        var slot = SelectedSlot.Value.Slot;
        if (SelectedOwner.Value == DispOwnerInfo.All) {
            FileList.Value = new ObservableCollection<FileEntryItem>(_dataBaseService.Entries.List(slot, true).Select(FromEntry));
        }
        else {
            var ownerId = SelectedOwner.Value.OwnerId;
            FileList.Value = new ObservableCollection<FileEntryItem>(_dataBaseService.Entries.List(slot, it => it.OwnerId == ownerId, true).Select(FromEntry));
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
                                    FileList.Value.Add(FromEntry(newEntry));
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
        string prevKey = string.IsNullOrEmpty(prevSortKey) ? (key == "Name" ? "Id" : key) : prevSortKey;
        if (prevSortKey == key) {
            ascending = !ascending;
        } else { 
            prevSortKey = key;
        }

        var list = FileList.Value;
        var sorted = ascending
            ? list.OrderBy((it) => it.GetType().GetProperty(key)?.GetValue(it)).ThenBy((it) => it.GetType().GetProperty(prevKey)?.GetValue(it))
            : list.OrderByDescending((it) => it.GetType().GetProperty(key)?.GetValue(it)).ThenBy((it) => it.GetType().GetProperty(prevKey)?.GetValue(it));
        FileList.Value = new ObservableCollection<FileEntryItem>(sorted);
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
            var stream = openEntryStream(entry.Entry);
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
                                        new ItemExtAttributes(entry.Entry.AttrDataDic));
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
