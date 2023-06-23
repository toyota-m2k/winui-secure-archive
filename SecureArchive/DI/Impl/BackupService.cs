using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design.Behavior;

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
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("duration")]
    public long Duration { get; set; }
}
internal class RemoteItemResponse {
    [JsonProperty("cmd")]
    public string Cmd { get; set; } = "";
    [JsonProperty("date")]
    public long Date { get; set; }
    [JsonProperty("list")]
    public List<RemoteItem> List { get; set; } = new();
}

internal class BackupService : IBackupService {
    private readonly ILogger _logger;
    private ISecureStorageService _secureStorageService;
    private BehaviorSubject<bool> _executing = new (false);
    private BehaviorSubject<RemoteItem?> _currentItem = new(null);
    private BehaviorSubject<IList<RemoteItem>?> _remoteItems = new(null);
    private BehaviorSubject<Exception?> _exception = new(null);
    private BehaviorSubject<int> _totalCount = new(0);
    private BehaviorSubject<int> _currentIndex = new(0);
    private BehaviorSubject<long> _totalBytes = new(0);
    private BehaviorSubject<long> _currentBytes = new(0);
    private CancellationTokenSource _cancellationTokenSource = new();

    private HttpClient? _httpClient;

    private HttpClient httpClient {
        get {
            if (_httpClient==null) { _httpClient = new HttpClient(); }
            return _httpClient;
        }
    }

    public bool IsBusy {
        get { lock(this) { return _executing.Value; } }
    }

    public IObservable<bool> Executing => _executing;
    public IObservable<IList<RemoteItem>?> RemoteItems => _remoteItems;

    public BackupService(ISecureStorageService secureStorageService, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<BackupService>();
        _secureStorageService = secureStorageService;
    }

    public bool Backup(string ownerId, string token, string address) {
        lock(this) {
            if(_executing.Value) {
                return false;
            }
            _executing.OnNext(true);
        }
        BackupProc(ownerId, token, address);
        return true;
    }


    public void Cancel() {
        lock (this) {
            _cancellationTokenSource.Cancel();
        }
    }

    private void Reset() {
        _remoteItems.OnNext(null);
        _exception.OnNext(null);
        _totalCount.OnNext(0);
        _currentIndex.OnNext(0);
        _totalBytes.OnNext(0);
        _currentBytes.OnNext(0);
        _cancellationTokenSource = new();
    }

    private void BackupProc(string ownerId, string token, string address) {
        Reset();
        Task.Run(async () => {
            try {
                var ct = _cancellationTokenSource.Token;
                var list = await GetList($"http://{address}/list?auth={token}");
                _totalCount.OnNext(list.Count);
                _currentIndex.OnNext(0);
                foreach (var item in list) {
                    ct.ThrowIfCancellationRequested();
                    _currentItem.OnNext(item);
                    _currentIndex.OnNext(_currentIndex.Value + 1);
                    if (!_secureStorageService.IsRegistered(ownerId, item.Id)) {
                        await GetVideo($"http://{address}/video?id={item.Id}&auth={token}", ownerId, item, ct);
                    }
                }
            }
            catch (Exception ex) {
                _exception.OnNext(ex);
            } finally {
                _executing.OnNext(false);
            }
        });
    }

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

    const int BUFF_SIZE = 1 * 1024 * 1024;
    private async Task GetVideo(string url, string ownerId, RemoteItem item, CancellationToken ct) {
        using (var response = (await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)).EnsureSuccessStatusCode()) {
            using (var content = response.Content)
            using (var inStream = await content.ReadAsStreamAsync())
            using (var entryCreator = await _secureStorageService.CreateEntry(ownerId, item.Id)) {
                if (entryCreator == null) { throw new InvalidOperationException("cannot create entry."); }
                var total = content.Headers.ContentLength ?? 0L;
                _totalBytes.OnNext(total);
                var buff = new byte[BUFF_SIZE];
                var recv = 0L;
                while (true) {
                    ct.ThrowIfCancellationRequested();
                    int len = await inStream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                    if (len == 0) {
                        entryCreator.Complete(item.Name, item.Size, item.Type, item.Date, null);
                        break;
                    }
                    recv += len;
                    _currentBytes.OnNext(recv);
                    await entryCreator.OutputStream.WriteAsync(buff, 0, len);
                    ct.ThrowIfCancellationRequested();
                }
            }
        }
    }


}
