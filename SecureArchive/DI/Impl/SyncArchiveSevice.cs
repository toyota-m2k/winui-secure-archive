using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Views;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SecureArchive.DI.Impl;
internal class SyncArchiveSevice : ISyncArchiveService {
    private readonly ILogger _logger;
    private ISecureStorageService _secureStorageService;
    private IDatabaseService _databaseService;
    private IPasswordService _passwordService;
    private ICryptographyService _cryptographyService;
    private IPageService _pageService;
    private IMainThreadService _mainThreadService;
    private IHttpClientFactory _httpClientFactory;

    private HttpClient httpClient => _httpClientFactory.CreateClient();
    private string peerAddress = "";
    private string challenge = "";
    private string authToken = "";
    private string hashedPwd = "";

    private CancellationTokenSource _cancellationTokenSource = null!;

    public SyncArchiveSevice(
        ISecureStorageService secureStorageService,
        IDatabaseService databaseService,
        IPasswordService passwordService,
        ICryptographyService cryptographyService,
        IPageService pageService,
        IMainThreadService mainThreadSercice,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory) {
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
        _passwordService = passwordService;
        _cryptographyService = cryptographyService;
        _pageService = pageService;
        _mainThreadService = mainThreadSercice;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<SyncArchiveSevice>();
    }

    private async Task<string> GetChallengeFromResponse(HttpResponseMessage response) {
        var jsonString = await response.Content.ReadAsStringAsync();
        var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
        if (json != null && json.ContainsKey("challenge")) {
            return json["challenge"];
        }
        throw new UnauthorizedAccessException("no challenge");
    }


    private async Task<bool> AuthWithToken(string token) {
        var url = $"http://{peerAddress}/auth/{token}";
        using (var response = await httpClient.GetAsync(url)) {
            if (response.StatusCode == HttpStatusCode.OK) {
                return true;
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized) {
                challenge = await GetChallengeFromResponse(response);
                return false;
            }
        }
        throw new UnauthorizedAccessException("no challenge");
    }

    private async Task<string> AuthWithPassPhrase(string passPhrase) {
        var content = new StringContent(passPhrase, Encoding.UTF8, "text/plain");
        var url = $"http://{peerAddress}/auth";
        using (var response = await httpClient.PutAsync(url, content)) {
            if(response.StatusCode == HttpStatusCode.OK) {
                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                if (json != null && json.ContainsKey("token")) {
                    return json["token"];
                }
            }
            else if(response.StatusCode == HttpStatusCode.Unauthorized) {
                challenge = await GetChallengeFromResponse(response);
                return "";
            }
        }
        throw new UnauthorizedAccessException("auth error");
    }


    private async Task<bool> RemoteAuth() {
        if (await AuthWithToken(authToken)) {
            return true;
        }
        // AuthWithTokenで challenge が設定されているはず
        if(challenge.IsEmpty()) {
            throw new UnauthorizedAccessException("no challenge");
        }

        authToken = "";
        var hpwd = hashedPwd;
        while (true) {
            if (hpwd.IsEmpty()) {
                var pwd = await _mainThreadService.Run(async () => {
                    return await App.GetService<RemotePasswordDialogPage>().GetPassword(_pageService.CurrentPage!.XamlRoot);
                });
                if (pwd == null) {
                    return false;
                }
                hpwd = _passwordService.CreateHashedPassword(pwd);
            }

            var passPhrase = _passwordService.CreatePassPhrase(challenge, hpwd);
            authToken = await AuthWithPassPhrase(passPhrase);
            if(authToken.IsNotEmpty()) {
                hashedPwd = hpwd;
                return true;
            }   
            hpwd = "";
        }
    }

    class FileEntryComparator : IEqualityComparer<FileEntry> {
        public bool Equals(FileEntry? x, FileEntry? y) {
            return y != null && x != null && x.OriginalId== y.OriginalId && x.OwnerId == y.OwnerId;
        }

        public int GetHashCode(FileEntry obj) {
            return (obj.OriginalId+obj.OwnerId).GetHashCode();
        }
    }

    private async Task<List<FileEntry>?> GetPeerList(bool retry=false) {
        var url = $"http://{peerAddress}/list?auth={authToken}&type=all&sync";
        bool needRetry = false;
        using (var response = await httpClient.GetAsync(url)) {
            if (response.IsSuccessStatusCode) {
                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                if (json != null && json.ContainsKey("list")) {
                    var list = json["list"] as JArray;
                    if (list != null) {
                        return list.Select(it => {
                            return FileEntry.FromDictionary((JObject)it);
                        }).ToList();
                    }
                }
            } else if(response.StatusCode == HttpStatusCode.Unauthorized) {
                if(await RemoteAuth() && !retry) {
                    needRetry = true;
                }
            }
        }
        if(needRetry) {
            return await GetPeerList(true);
        }
        return null;
    }


    private async Task<bool> UploadEntry(FileEntry entry) {
        try {
            using (var fileStream = _secureStorageService.OpenEntry(entry)) {
                var body = new StreamContent(fileStream);
                body.Headers.ContentType = new MediaTypeHeaderValue(entry.Type == "mp4" ? "video/mp4" : "image/jpeg");

                var content = new MultipartFormDataContent();
                content.Add(new StringContent(entry.OwnerId), "OwnerId");
                content.Add(new StringContent(entry.OriginalId), "OriginalId");
                content.Add(new StringContent($"{entry.OriginalDate}"), "FileDate");
                content.Add(new StringContent(entry.MetaInfo ?? ""), "MetaInfo");
                content.Add(body, "File", entry.Name);
                var url = $"http://{peerAddress}/upload";
                using (var response = await httpClient.PostAsync(url, content)) {
                    return response.IsSuccessStatusCode;
                }
            }
        } catch(Exception e) {
            _logger.Error(e);
            return false;
        }
    }

    private const int BUFF_SIZE = 1 * 1024 * 1024;

    private async Task<bool> DownloadEntry(FileEntry entry, ProgressProc? progress) {
        string type = entry.Type == "mp4" ? "video" : "photo";
        string url = $"http://{peerAddress}/{type}/?id={entry.Id}&auth={authToken}";
        var ct = _cancellationTokenSource.Token;    
        try {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                if (!response.IsSuccessStatusCode) return false;
                using (var content = response.Content)
                using (var inStream = await content.ReadAsStreamAsync())
                using (var entryCreator = await _secureStorageService.CreateEntry(entry.OwnerId, entry.OriginalId, true)) {
                    if (entryCreator == null) { throw new InvalidOperationException("cannot create entry."); }
                    var total = content.Headers.ContentLength ?? 0L;
                    var buff = new byte[BUFF_SIZE];
                    var recv = 0L;
                    while (true) {
                        ct.ThrowIfCancellationRequested();
                        int len = await inStream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                        if (len == 0) {
                            entryCreator.Complete(entry.Name, entry.Size, entry.Type, entry.OriginalDate, entry.MetaInfo);
                            break;
                        }
                        recv += len;
                        await entryCreator.OutputStream.WriteAsync(buff, 0, len);
                        progress?.Invoke(recv, total);
                        ct.ThrowIfCancellationRequested();
                    }
                    _logger.Debug("downloaded {0}", entry.Name);
                }
            }
            return true;
        }
        catch (Exception e) {
            _logger.Error(e, "download error");
            return false;
        }
    }

    private struct Pair {
        public FileEntry peer;
        public FileEntry my;
    }

    public void Start(string peerAddress) {
        _cancellationTokenSource = new CancellationTokenSource();
        this.peerAddress = peerAddress;
        Task.Run(async () => {
            try {
                if(!await RemoteAuth()) {
                    return;
                }
                var myList = _databaseService.Entries.List(false);
                var peerList = await GetPeerList();
                if(peerList == null) {
                    return;
                }
                var comparator = new FileEntryComparator();
                var peerNewFile = peerList.Except(myList, comparator).ToList();
                var myNewFile = myList.Except(peerList, comparator).ToList();
                var myCommonFileDic = myList.Intersect(peerList, comparator).Aggregate(new Dictionary<string, FileEntry>(), (dic, entry) => { dic.Add(entry.Name+entry.OwnerId, entry); return dic; });
                var commonPair = peerList.Intersect(myList, comparator).Select(it => new Pair { peer = it, my = myCommonFileDic[it.Name + it.OwnerId] }).Where(it => it.peer.OriginalDate != it.my.OriginalDate).ToList();

                //var peerCommonFile = peerList.Intersect(myList, comparator).ToList();
                
                //var peerUpdateFile = peerCommonFile.Where(it => it.OriginalDate > myCommonFileDic[it.Name+it.OwnerId].OriginalDate);
                //var myUpdateFile = peerCommonFile.Where(it=> it.OriginalDate < myCommonFileDic[it.Name+it.OwnerId].OriginalDate).Select(it=>myCommonFileDic[it.Name+it.OwnerId]);

                int counter = 0;
                foreach (var entry in peerNewFile) {
                    counter++;
                    _logger.Debug($"Download (NEW): [{counter}/{peerNewFile.Count}] {entry.Name}");
                    //await DownloadEntry(entry, null);
                }
                counter = 0;
                foreach(var pair in commonPair.Where(it=>it.peer.OriginalDate > it.my.OriginalDate)) {
                    counter++;
                    _logger.Debug($"Download (UPD):  [{counter}/{peerNewFile.Count}] {pair.my.Name}: {new DateTime(pair.my.OriginalDate)} <-- {new DateTime(pair.peer.OriginalDate)}");
                    //await DownloadEntry(pair.peer, null);
                }

                counter = 0;
                foreach (var entry in myNewFile) {
                    counter++;
                    _logger.Debug($"Upload (NEW): [{counter}/{peerNewFile.Count}] {entry.Name}");
                    //await UploadEntry(entry);
                }
                counter = 0;
                foreach (var pair in commonPair.Where(it => it.peer.OriginalDate > it.my.OriginalDate)) {
                    counter++;
                    _logger.Debug($"Upload (UPD):  [{counter}/{peerNewFile.Count}] {pair.my.Name}: {new DateTime(pair.my.OriginalDate)} --> {new DateTime(pair.peer.OriginalDate)}");
                    //await UploadEntry(pair.my);
                }

            }
            catch (Exception ex) {
                _logger.Error(ex);
            }
        });
    }
}
