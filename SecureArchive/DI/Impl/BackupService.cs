using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using SecureArchive.Views;
using System.Diagnostics;
using System.Text;

namespace SecureArchive.DI.Impl;

internal class RemoteItem {
    [JsonProperty("id")]
    public string Id { get; set; } = "";
    [JsonProperty("name")]
    public string Name { get; set; } = "";
    [JsonProperty("size")]
    public long Size { get; set; }
    [JsonProperty("date")]
    public long Date { get; set; }              // ファイルのタイムスタンプ --> LastModifiedDate
    [JsonProperty("creationDate")]
    public long CreationDate { get; set; }      // 動画を撮影した日時 = ファイル名から取り出される日付
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("duration")]
    public long Duration { get; set; }
    [JsonProperty("cloud")]
    public long Cloud { get; set; } = 0;
    [JsonProperty("attrDate")]
    public long ExtAttrDate { get; set; } = 0;
    [JsonProperty("slot")]
    public int Slot { get; set; } = 0;          // スロット番号（0:未登録、1:登録済み）

    [JsonIgnore]
    public string UrlType => (Type == "mp4" || Type == ".mp4") ? "video" : "photo";
}
internal class RemoteItemResponse {
    [JsonProperty("cmd")]
    public string Cmd { get; set; } = "";
    [JsonProperty("date")]
    public long Date { get; set; }
    [JsonProperty("list")]
    public List<RemoteItem> List { get; set; } = new();
}

internal class BackupCompletion {
    [JsonProperty("auth")]
    public string AuthToken { get; set; } = "";
    [JsonProperty("slot")]
    public int Slot { get; set; } = 0;
    [JsonProperty("id")]
    public string Id { get; set; } = "";
    [JsonProperty("slots")]
    public List<int>? Slots { get; set; } = null;
    [JsonProperty("ids")]
    public List<string>? Ids { get; set; } = null;
    [JsonProperty("owner")]
    public string OwnerId { get; set; } = "";
    [JsonProperty("status")]
    public bool Status { get; set; } = false;
}

internal class BackupService : IBackupService {
    private readonly UtLog _logger = UtLog.Instance(typeof(BackupService));
    private ISecureStorageService _secureStorageService;
    private IPageService _pageService;
    private IMainThreadService _mainThreadService;
    private IHttpClientFactory _httpClientFactory;
    private IDatabaseService _databaseService;
    private IDeviceMigrationService _deviceMigrationService;

    //private BehaviorSubject<Status> _executing = new (Status.NONE);
    //private BehaviorSubject<RemoteItem?> _currentItem = new(null);
    //private BehaviorSubject<int> _totalCount = new(0);
    //private BehaviorSubject<int> _currentIndex = new(0);
    //private BehaviorSubject<long> _totalBytes = new(0);
    //private BehaviorSubject<long> _currentBytes = new(0);
    //private CancellationTokenSource _cancellationTokenSource = new();

    //public Exception? LastError { get; private set; } = null;

    private bool _isBusy = false;

    //private HttpClient? _httpClient;

    private HttpClient httpClient => _httpClientFactory.CreateClient();

    //public Status GetStatus() {
    //    lock(this) {
    //        return _executing.Value;
    //    }
    //}       
    //public bool IsBusy {
    //    get { lock(this) { return _executing.Value!=Status.NONE; } }
    //}
    //public bool IsDownloading {
    //    get { lock (this) { return _executing.Value != Status.DOWNLOADING; } }
    //}
    //public bool IsListing{
    //    get { lock (this) { return _executing.Value != Status.LISTING; } }
    //}

    //public IObservable<Status> Executing => _executing;
    public IList<RemoteItem> RemoteNewItems { get; private set; } = null!;
    public IList<FileEntry> RemoteRemovedItems { get; private set; } = null!;
    public IList<FileEntry> RemoteModifiedItems { get; private set; } = null!;


    //public IObservable<RemoteItem?> CurrentItem => _currentItem;

    //public IObservable<int> TotalCount => _totalCount;

    //public IObservable<int> CurrentIndex => _currentIndex;

    //public IObservable<long> TotalBytes => _totalBytes;

    //public IObservable<long> CurrentBytes => _currentBytes;

    private string F(long size) {
        return string.Format("{0:#,0}", size);
    }

    public BackupService(
        ISecureStorageService secureStorageService, 
        IPageService pageService, 
        IMainThreadService mainThreadSercice, 
        IHttpClientFactory httpClientFactory,
        IDatabaseService databaseService,
        ILoggerFactory loggerFactory,
        IDeviceMigrationService deviceMigrationService
        ) {
        _secureStorageService = secureStorageService;
        _pageService = pageService;
        _mainThreadService = mainThreadSercice;
        _httpClientFactory = httpClientFactory;
        _databaseService = databaseService;
        _deviceMigrationService = deviceMigrationService;
    }

    private string OwnerId = null!;
    private string Token = null!;
    private string Address = null!;


    public bool Request(string ownerId, string token, string address) {
        lock(this) {
            if(_isBusy) { 
                _logger.Warn("cannot accept new backup request, because it is busy.");
                return false;
            }
            _isBusy = true;
        }
        OwnerId = ownerId;
        Token = token;
        Address = address;
        _ = ListProc();
        return true;
    }

    //public async Task BeginBackup(IList<RemoteItem> targets) {
    //    lock(this) {
    //        if(_executing.Value != Status.NONE) {
    //            return;
    //        }
    //        _executing.OnNext(Status.DOWNLOADING);
    //    }
    //    await BackupProc(targets);
    //}


    private void Reset() {
        RemoteNewItems = null!;
        RemoteRemovedItems = null!;
        RemoteModifiedItems = null!;
    }

    enum CloudState {
        Local = 0,      // ファイルはスマホのローカルにのみ存在
        Uploaded = 1,   // アップロード済（ファイルはローカルとサーバーの両方に存在）
        Cloud = 2,      // ファイルはサーバー(SecureArchive)にのみ存在
    }

    private string GetSlotId(int slot) {
        return $"slot{slot}/";
    }

    private string itemKey(RemoteItem item) {
        return $"{item.Slot}/{item.Id}";
    }
    private string itemKey(FileEntry item) {
        return $"{item.Slot}/{item.OriginalId}";
    }

    private async Task<bool> ListProc() {         
        Reset();
        return await Task.Run(async () => {
            try {
                // リモート（モバイル端末）側のファイルリストを取得
                // 移行済みのアイテムは除外する（リモート上に存在しないものとして扱う）
                // SecureArchive 側では、移行済みアイテムのOwnerIdは、すでに別のデバイスのものになっているので、辻褄は合うはず。
                var rawList = (await GetList($"http://{Address}/list?auth={Token}&type=all&backup"))
                    .Where(it => !_deviceMigrationService.IsMigrated(OwnerId, it.Slot, it.Id));

                // Id (OriginalId) --> RemoteItem のマップを作成
                var remoteMap = rawList.ToDictionary(it => itemKey(it));

                // durationフィールドを補完する
                _databaseService.EditEntry(entries => {
                    var noDurations = entries.List(-1, it => it.Duration == 0 && it.Type == "mp4" && it.OwnerId == OwnerId, false);
                    var result = false;
                    if (noDurations.Count > 0) {
                        foreach (var e in noDurations) {
                            if (remoteMap.TryGetValue(itemKey(e), out var item)) {
                                e.Duration = item.Duration;
                                result = true;
                            }
                        }
                    }
                    return result;
                });

                /**
                 * リモート（モバイル端末）側で「ローカル（モバイル端末）にだけ存在する」と思っているが、
                 * 実は、SecureArchive にアップロード済み、というものがあれば、ここでバックアップ完了通知を送っておく。
                 */
                var registeredList = rawList.Where(it => it.Cloud==(int)CloudState.Local && _secureStorageService.IsRegistered(OwnerId, it.Slot, it.Id, it.Date)).ToArray();
                if(registeredList.Length>0) {
                    await NotifyCompletion(registeredList);
                }

                /**
                 * リモートで削除されたファイル
                 * （ローカルにだけ存在するファイル。ただし、削除フラグがついているものは除く。）
                 */
                var removedList = _secureStorageService.GetList(OwnerId, -1, (it) => {
                    return !it.IsDeleted && !remoteMap.ContainsKey(itemKey(it));
                });

                // 初期バージョンの不具合で、LastModifiedDate/CreationDate が 0 になっているものがある。
                _databaseService.EditEntry((entries) => {
                    var list = entries.List(-1, e => e.LastModifiedDate == 0 || e.CreationDate == 0, false);
                    if(list==null || list.FirstOrDefault()==null) return false;
                    bool modified = false;
                    foreach(var e in list) {
                        var item = remoteMap[itemKey(e)];
                        if (item != null && (e.LastModifiedDate!=item.Date || e.CreationDate!=item.Date)) {
                            e.LastModifiedDate = item.Date;
                            e.CreationDate = item.CreationDate;
                            modified = true;
                        }
                    }
                    return modified;
                });


                /**
                 * リモートで変更されたファイルのリスト
                 */
                var modifiedList = _secureStorageService.GetList(OwnerId, -1, (it) => {
                    return !it.IsDeleted && remoteMap.ContainsKey(itemKey(it)) && it.ExtAttrDate != remoteMap[itemKey(it)].ExtAttrDate;
                });

                /**
                 * リモート側で追加または更新されたファイルのリスト
                 */
                var newList = rawList.Where(it => it.Cloud!=(int)CloudState.Cloud && !_secureStorageService.IsRegistered(OwnerId, it.Slot, it.Id, it.Date)).ToList();

                /**
                 * 変更がなければメッセージを表示して終了
                 */
                if(newList.Count==0 && removedList.Count==0 && modifiedList.Count==0) {
                    await MessageBoxBuilder.Create(App.MainWindow)
                        .SetTitle("Backup")
                        .SetMessage("Everything is up to date.")
                        .AddButton("OK")
                        .ShowAsync();
                    return false;
                }


                //foreach (var f in newList) {
                //    _logger.Debug($"Appending: {f.Id} - {f.Name}");
                //}
                //foreach (var f in removedList) {
                //    _logger.Debug($"Removing: {f.OriginalId} - {f.Name}");
                //}

                RemoteNewItems = newList;
                RemoteRemovedItems = removedList;
                RemoteModifiedItems = modifiedList;
                await _mainThreadService.Run(async () => {
                    if (newList.Count > 0) {
                        await CustomDialogBuilder<BackupDialogPage, bool>
                            .Create(_pageService.CurrentPage!.XamlRoot, new BackupDialogPage())
                            .SetTitle("Backup")
                            .ShowAsync();
                    }

                    if (removedList.Count > 0) {
                        await CustomDialogBuilder<DeleteBackupDialogPage, bool>
                            .Create(_pageService.CurrentPage!.XamlRoot, new DeleteBackupDialogPage())
                            .SetTitle("Delete Files Removed on the Device")
                            .ShowAsync();
                    }
                    if (modifiedList.Count > 0 ) {
                        await CustomDialogBuilder<UpdateBackupDialogPage, bool>
                            .Create(_pageService.CurrentPage!.XamlRoot, new UpdateBackupDialogPage())
                            .SetTitle("Update Files Modified on the Device")
                            .ShowAsync();
                    }


                    //var dialog = new ContentDialog();

                    //// XamlRoot must be set in the case of a ContentDialog running in a Desktop app
                    //dialog.XamlRoot = _pageService.CurrentPage!.XamlRoot;
                    //dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                    //dialog.Title = "Backup";
                    ////dialog.PrimaryButtonText = "Backup";
                    ////dialog.SecondaryButtonText = "Don't Save";
                    //dialog.CloseButtonText = "Close";
                    //dialog.DefaultButton = ContentDialogButton.Close;
                    //dialog.Content = new BackupDialogPage();

                    //await dialog.ShowAsync();
                });
                return true;
            }
            catch (Exception ex) {
                _logger.Error(ex);
                return false;
            }
            finally {
                lock (this) {
                    _isBusy = false;
                }
            }
        });
    }



    //private async Task BackupTarget(IList<RemoteItem> targets, CancellationToken cancellationToken) {
    //    await Task.Run(async () => {
    //        try {
    //            _totalCount.OnNext(targets.Count);
    //            _currentIndex.OnNext(0);
    //            foreach (var item in targets) {
    //                ct.ThrowIfCancellationRequested();
    //                _currentItem.OnNext(item);
    //                _currentIndex.OnNext(_currentIndex.Value + 1);
    //                if (!_secureStorageService.IsRegistered(OwnerId, item.Id)) {
    //                    await DownloadTarget($"http://{Address}/{item.UrlType}/?id={item.Id}&auth={Token}", OwnerId, item, ct);
    //                }
    //            }
    //        }
    //        catch (Exception ex) {
    //            LastError = ex;
    //        } finally {
    //            lock(this) {
    //                _executing.OnNext(Status.NONE);
    //            }
    //        }
    //    });
    //}

    private async Task<List<RemoteItem>> GetList(string url) {
        using (var response = (await httpClient.GetAsync(url)).EnsureSuccessStatusCode()) {
            var json = await response.Content.ReadAsStringAsync();
            var dic = JsonConvert.DeserializeObject<RemoteItemResponse?>(json);
            if (dic == null) {
                throw new InvalidDataException("bad json");
            }
            return dic.List;
            //var list = dic["list"] as Newtonsoft.Json.Linq.JArray;
            //if (list == null) {
            //    throw new InvalidDataException("no list");
            //}
            //return list.Select(o => {
            //    var d = o as JObject; //o as Dictionary<string, string>;
            //    if (d == null) { throw new InvalidCastException("bad entry"); }
            //    return new RemoteItem {
            //        Id = (string)((JValue)d["id"]).Value,
            //        Name = d["name"],
            //        Size = Convert.ToInt64(d["size"]),
            //        Date = Convert.ToInt64(d["date"]),
            //        Type = d["type"],
            //        Duration = Convert.ToInt64(d.GetValue("duration"))
            //    };
            //}).ToList();
        }
    }

    private async Task<bool> NotifyCompletion(params RemoteItem[] items) {
        if(items.Length==0) return false;

        BackupCompletion bc;
        if (items.Length == 1) {
            bc = new BackupCompletion() { AuthToken = Token, Slot=items[0].Slot, Id = items[0].Id, OwnerId = OwnerId, Status = true };
        } else {
            bc = new BackupCompletion() { AuthToken = Token, Ids = items.Select(it=>it.Id).ToList(), Slots=items.Select(it=>it.Slot).ToList(), OwnerId = OwnerId, Status = true };
        }
        var url = $"http://{Address}/backup/done";
        var json = JsonConvert.SerializeObject(bc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try {
            using (var response = await httpClient.PutAsync(url, content)) {
                if (!response.IsSuccessStatusCode) {
                    return false;
                }
                return true;
            }
        } catch(Exception ex) {
            _logger.Error(ex);
            return false;
        }
    }


    private const int BUFF_SIZE = 1 * 1024 * 1024;
    public async Task<bool> DownloadTarget(RemoteItem item, ProgressProc progress, CancellationToken ct) {
        var url = $"http://{Address}/{GetSlotId(item.Slot)}{item.UrlType}?id={item.Id}&auth={Token}";
        try {
            await Task.Delay(500);          // これを入れないと、Pixel3 でエラーになる。
            var extAttr = await GetExtAttributes(item.Slot, item.Id, ct);
            //if (extAttr == null) return false;

            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                if (!response.IsSuccessStatusCode) return false;
                using (var content = response.Content)
                using (var inStream = await content.ReadAsStreamAsync())
                using (var entryCreator = await _secureStorageService.CreateEntry(OwnerId, item.Slot, item.Id, true)) {
                    if (entryCreator == null) { throw new InvalidOperationException("cannot create entry."); }
                    var total = content.Headers.ContentLength ?? 0L;
                    var buff = new byte[BUFF_SIZE];
                    var recv = 0L;
                    var ok = false;
                    while (true) {
                        ct.ThrowIfCancellationRequested();
                        int len = await inStream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                        if (len == 0) {
                            if (total == 0L || total == recv) {
                                entryCreator.Complete(item.Name, total!=0L ? total : item.Size, item.Type, item.Date, item.CreationDate, item.Duration, null, extAttr);
                                ok = true;
                            } else {
                                // 受信したデータファイルのサイズが不正（途中で切れた、とか？）
                                throw new Exception($"{item.Name} : invalid length (req: {F(total)} actual: {F(recv)}");
                            }
                            break;
                        }
                        recv += len;
                        await entryCreator.OutputStream.WriteAsync(buff, 0, len);
                        progress(recv, total);
                        ct.ThrowIfCancellationRequested();
                    }
                    _logger.Debug($"download ({ok}): {item.Name} -- total length = {F(total)}");
                }
            }
            await Task.Delay(500);          // これを入れないと、Pixel3 でエラーになる。
            return await NotifyCompletion(item);
        }
        catch (Exception ex) {
            _logger.Error(ex);
            return false;
        }
    }

    public Task<bool> DeleteBackupEntry(FileEntry entry) {
        return _secureStorageService.DeleteEntry(entry, deleteDbEntry:false);
    }


    private async Task<IItemExtAttributes?> GetExtAttributes(int slot, string originalId, CancellationToken ct) {
        var url = $"http://{Address}/{GetSlotId(slot)}extension?id={originalId}&auth={Token}";
        try {
            using (var response = await httpClient.GetAsync(url, ct)) {
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync();
                var dic = ItemExtAttributes.FromJson(json);
                if (dic == null) {
                    throw new InvalidDataException("bad json");
                }
                return dic;
            }
        }
        catch (Exception ex) {
            _logger.Error(ex);
            return null;
        }
    }

    public async Task<bool> UpdateBackupEntry(FileEntry entry, CancellationToken ct) {
        var dic = await GetExtAttributes(entry.Slot, entry.OriginalId, ct);
        if(dic == null) return false;
        return _databaseService.EditEntry(entries => {
            var e = entries.GetById(entry.Id);
            if(e==null) return false;
            e.ExtAttrDate = dic.ExtAttrDate;
            e.Rating = dic.Rating;
            e.Mark = dic.Mark;
            e.Label = dic.Label;
            e.Category = dic.Category;
            e.Chapters = dic.Chapters;
            return true;    // modified
        });
    }
}
