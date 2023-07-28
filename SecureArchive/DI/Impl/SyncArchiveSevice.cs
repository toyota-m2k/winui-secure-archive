using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Views;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

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
    private string rawPassword = "";
    private string hashedPwd = "";

    private CancellationTokenSource? _cancellationTokenSource = null;

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
        authToken = "";

        // AuthWithTokenで challenge が設定されているはず
        if (challenge.IsEmpty()) {
            throw new UnauthorizedAccessException("no challenge");
        }

        hashedPwd = _passwordService.CreateHashedPassword(rawPassword);
        var passPhrase = _passwordService.CreatePassPhrase(challenge, hashedPwd);
        authToken = await AuthWithPassPhrase(passPhrase);
        if (authToken.IsNotEmpty()) {
            return true;
        } else {
            return false;
        }


        //while (true) {
        //    if (rawPassword.IsEmpty()) {
        //        var pwd = await _mainThreadService.Run(async () => {
        //            return await App.GetService<RemotePasswordDialogPage>().GetPassword(Parent?.GetValue()??_pageService.CurrentPage!.XamlRoot);
        //        });
        //        if (pwd == null) {
        //            return false;
        //        }
        //        rawPassword = pwd;
        //    }
        //    hashedPwd = _passwordService.CreateHashedPassword(rawPassword);
        //    var passPhrase = _passwordService.CreatePassPhrase(challenge, hashedPwd);
        //    authToken = await AuthWithPassPhrase(passPhrase);
        //    if(authToken.IsNotEmpty()) {
        //        return true;
        //    }
        //    rawPassword = "";
        //}
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


    private async Task<bool> UploadEntry(FileEntry entry, ProgressProc progress, CancellationToken ct) {
        try {
            using (var fileStream = _secureStorageService.OpenEntry(entry)) {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(entry.Type == "mp4" ? "video/mp4" : "image/jpeg");
                fileContent.Headers.ContentLength = entry.Size;
                var body = new ProgressableStreamContent(fileContent, 4096, (sent, total) => {
                    progress(sent, total ?? entry.Size);
                });

                var content = new MultipartFormDataContent {
                    { new StringContent(entry.OwnerId), "OwnerId" },
                    { new StringContent(entry.OriginalId), "OriginalId" },
                    { new StringContent($"{entry.OriginalDate}"), "FileDate" },
                    { new StringContent($"{entry.CreationDate}"), "CreationDate" },
                    { new StringContent(entry.MetaInfo ?? ""), "MetaInfo" },
                    { body, "File", entry.Name }
                };
                var url = $"http://{peerAddress}/upload";
                using (var response = await httpClient.PostAsync(url, content, ct)) {
                    return response.IsSuccessStatusCode;
                }
            }
        } catch(Exception e) {
            _logger.Error(e);
            return false;
        }
    }

    private const int BUFF_SIZE = 1 * 1024 * 1024;

    private async Task<bool> DownloadEntry(FileEntry entry, ProgressProc progress, CancellationToken ct) {
        string type = entry.Type == "mp4" ? "video" : "photo";
        string url = $"http://{peerAddress}/{type}?id={entry.Id}&auth={authToken}";
        progress(0, entry.Size);
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
                            entryCreator.Complete(entry.Name, entry.Size, entry.Type, entry.OriginalDate, entry.CreationDate, entry.MetaInfo);
                            break;
                        }
                        recv += len;
                        await entryCreator.OutputStream.WriteAsync(buff, 0, len);
                        progress(recv, total);
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

    WeakReference<XamlRoot>? Parent=null;
    Regex regAddress = new Regex(@"(?:(?<ip>\d+\.\d+\.\d+\.\d+)|(?<name>[a-zA-Z]+\w*))(?::(?<port>\d+))?");

    public async Task<bool> Start(
        string peerAddress, 
        string peerPassword, 
        XamlRoot? parent, 
        ErrorMessageProc errorMessageProc,
        SyncStateProc syncTaskProc, 
        ProgressProc countProgress, 
        ProgressProc byteProgress) {
        if(parent!=null) {
            Parent = new WeakReference<XamlRoot>(parent);
        }
        _cancellationTokenSource = new CancellationTokenSource();
        var ct = _cancellationTokenSource.Token;

        void errorProc(string message, bool fatal) {
            _mainThreadService.Run(() => {
                errorMessageProc(message, fatal);
            });
        }

        var reg = regAddress.Match(peerAddress);
        if(!reg.Success) {
            errorProc("Wrong Peer Address.", true);
            return false;
        }
        var ip = reg.Groups["ip"];
        var name = reg.Groups["name"];
        var port = reg.Groups["port"];
        if(ip.Success) {
            this.peerAddress = peerAddress;
        } else if (name.Success) {
            var ips = Dns.GetHostAddresses(name.Value);
            if (ips == null) {
                errorProc("Cannot resolve IP address.", true);
                return false;
            }
            var ipv4addr = ips.Where(it=>it.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();
            if (ipv4addr == null) {
                errorProc("Bad peer address.", true);
                return false;
            }
            this.peerAddress = $"{ipv4addr}:{port}";
        } else {
            errorProc("Invalid peer address.", true);
            return false;
        }


        this.rawPassword = peerPassword;
        return await Task.Run(async () => {
            try {
                if(!await RemoteAuth()) {
                    errorProc("Authentication Error.", false);
                    return false;
                }
                var myList = _databaseService.Entries.List(false);
                var peerList = await GetPeerList();
                if(peerList == null) {
                    errorProc("No peer items.", false);
                    return false;
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
                    ct.ThrowIfCancellationRequested();
                    counter++;
                    _logger.Debug($"Download (NEW): [{counter}/{peerNewFile.Count}] {entry.Name}");
                    countProgress(counter, peerNewFile.Count);
                    await DownloadEntry(entry, byteProgress, ct);
                }
                
                counter = 0;
                var peerUpdates = commonPair.Where(it => it.peer.OriginalDate > it.my.OriginalDate).ToList();
                foreach (var pair in peerUpdates) {
                    ct.ThrowIfCancellationRequested();
                    counter++;
                    _logger.Debug($"Download (UPD):  [{counter}/{peerUpdates.Count}] {pair.my.Name}: {new DateTime(pair.my.OriginalDate)} <-- {new DateTime(pair.peer.OriginalDate)}");
                    countProgress(counter, peerUpdates.Count);
                    await DownloadEntry(pair.peer, byteProgress, ct);
                }

                counter = 0;
                foreach (var entry in myNewFile) {
                    ct.ThrowIfCancellationRequested();
                    counter++;
                    _logger.Debug($"Upload (NEW): [{counter}/{myNewFile.Count}] {entry.Name}");
                    countProgress(counter, myNewFile.Count);
                    await UploadEntry(entry, byteProgress, ct);
                }
                counter = 0;
                var myUpdates = commonPair.Where(it => it.peer.OriginalDate > it.my.OriginalDate).ToList();
                foreach (var pair in myUpdates) {
                    ct.ThrowIfCancellationRequested();
                    counter++;
                    _logger.Debug($"Upload (UPD):  [{counter}/{myUpdates.Count}] {pair.my.Name}: {new DateTime(pair.my.OriginalDate)} --> {new DateTime(pair.peer.OriginalDate)}");
                    countProgress(counter, myUpdates.Count);
                    await UploadEntry(pair.my, byteProgress, ct);
                }
                return true;
            }
            catch (Exception ex) {
                _logger.Error(ex);
                errorProc("Sync Error.", true);
                return false;
            }
        });
    }

    public void Cancel() {
        _cancellationTokenSource?.Cancel();
    }
}
