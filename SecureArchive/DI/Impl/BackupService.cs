using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureArchive.Utils;
using SecureArchive.Views;
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
    public long Date { get; set; }
    [JsonProperty("creationDate")]
    public long CreationDate { get; set; }
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("duration")]
    public long Duration { get; set; }
    [JsonProperty("cloud")]
    public long Cloud { get; set; } = 0;

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
    [JsonProperty("id")]
    public string Id { get; set; } = "";
    [JsonProperty("ids")]
    public List<string>? Ids { get; set; } = null;
    [JsonProperty("owner")]
    public string OwnerId { get; set; } = "";
    [JsonProperty("status")]
    public bool Status { get; set; } = false;
}

internal class BackupService : IBackupService {
    private readonly ILogger _logger;
    private ISecureStorageService _secureStorageService;
    private IPageService _pageService;
    private IMainThreadService _mainThreadService;
    private IHttpClientFactory _httpClientFactory;

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
    public IList<RemoteItem> RemoteItems { get; private set; } = null!;

    //public IObservable<RemoteItem?> CurrentItem => _currentItem;

    //public IObservable<int> TotalCount => _totalCount;

    //public IObservable<int> CurrentIndex => _currentIndex;

    //public IObservable<long> TotalBytes => _totalBytes;

    //public IObservable<long> CurrentBytes => _currentBytes;

    public BackupService(
        ISecureStorageService secureStorageService, 
        IPageService pageService, 
        IMainThreadService mainThreadSercice, 
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<BackupService>();
        _secureStorageService = secureStorageService;
        _pageService = pageService;
        _mainThreadService = mainThreadSercice;
        _httpClientFactory = httpClientFactory;
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
        RemoteItems = null!;
    }

    private async Task<bool> ListProc() {         
        Reset();
        return await Task.Run(async () => {
            try {
                var rawList = await GetList($"http://{Address}/list?auth={Token}&type=all&backup");
                if(rawList.Count==0) {
                    return false;
                }
                var registeredList = rawList.Where(it => it.Cloud==0 && _secureStorageService.IsRegistered(OwnerId, it.Id, it.Date)).ToArray();
                if(registeredList.Length>0) {
                    await NotifyCompletion(registeredList);
                }


                var newList = rawList.Where(it => !_secureStorageService.IsRegistered(OwnerId, it.Id, it.Date)).ToList();
                if(newList.Count==0) {
                    return false;
                }

                RemoteItems = newList;
                await _mainThreadService.Run(async () => {
                    await CustomDialogBuilder<BackupDialogPage,bool>
                        .Create(_pageService.CurrentPage!.XamlRoot, new BackupDialogPage())
                        .SetTitle("Backup")
                        .ShowAsync();


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
            bc = new BackupCompletion() { AuthToken = Token, Id = items[0].Id, OwnerId = OwnerId, Status = true };
        } else {
            bc = new BackupCompletion() { AuthToken = Token, Ids = items.Select(it=>it.Id).ToList(), OwnerId = OwnerId, Status = true };
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
        var url = $"http://{Address}/{item.UrlType}?id={item.Id}&auth={Token}";
        try {
            await Task.Delay(500);          // これを入れないと、Pixel3 でエラーになる。
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                if (!response.IsSuccessStatusCode) return false;
                using (var content = response.Content)
                using (var inStream = await content.ReadAsStreamAsync())
                using (var entryCreator = await _secureStorageService.CreateEntry(OwnerId, item.Id, true)) {
                    if (entryCreator == null) { throw new InvalidOperationException("cannot create entry."); }
                    var total = content.Headers.ContentLength ?? 0L;
                    var buff = new byte[BUFF_SIZE];
                    var recv = 0L;
                    while (true) {
                        ct.ThrowIfCancellationRequested();
                        int len = await inStream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                        if (len == 0) {
                            entryCreator.Complete(item.Name, item.Size, item.Type, item.Date, item.CreationDate, null);
                            break;
                        }
                        recv += len;
                        await entryCreator.OutputStream.WriteAsync(buff, 0, len);
                        progress(recv, total);
                        ct.ThrowIfCancellationRequested();
                    }
                    _logger.Debug("downloaded {0}", item.Name);
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

}
