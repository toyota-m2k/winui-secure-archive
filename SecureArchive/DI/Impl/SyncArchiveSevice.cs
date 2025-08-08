using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace SecureArchive.DI.Impl;
internal class SyncArchiveSevice : ISyncArchiveService {
    private readonly UtLog _logger;
    private ISecureStorageService _secureStorageService;
    private IDatabaseService _databaseService;
    private IPasswordService _passwordService;
    private ICryptographyService _cryptographyService;
    private IPageService _pageService;
    private IMainThreadService _mainThreadService;
    private IHttpClientFactory _httpClientFactory;
    private IDeviceMigrationService _deviceMigrationService;

    /**
     * デフォルトのタイムアウト（100秒）が設定された HttpClient
     * このタイムアウトは、要求全体に対するタイムアウトであり、接続の確立と要求本体の送受信の間を区別しないので要注意。
     * ファイルのアップロードやダウンロードのように、要求本体の送受信に時間がかかる場合は、Timeout.InfiniteTimeSpan を設定した infiniteHttpClient を使うこと。
     */
    private HttpClient? _defaultClient = null;
    private HttpClient getDefaultHttpClient() {
        if (_defaultClient == null) {
            _defaultClient = _httpClientFactory.CreateClient();
            _defaultClient.Timeout = TimeSpan.FromSeconds(100); // デフォルトのタイムアウトを100秒に設定
        }
        return _defaultClient;
    }
    /**
     * タイムアウト無しの HttpClient
     */
    private HttpClient? _infiniteClient = null;
    private HttpClient getInfiniteHttpClient() {
        if (_infiniteClient == null) {
            _infiniteClient = _httpClientFactory.CreateClient();
            _infiniteClient.Timeout = Timeout.InfiniteTimeSpan; // タイムアウト無し
        }
        return _infiniteClient;
    }

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
        IDeviceMigrationService deviceMigrationService
        ) {
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
        _passwordService = passwordService;
        _cryptographyService = cryptographyService;
        _pageService = pageService;
        _mainThreadService = mainThreadSercice;
        _httpClientFactory = httpClientFactory;
        _deviceMigrationService = deviceMigrationService;
        _logger = UtLog.Instance(typeof(SyncArchiveSevice));
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
        using (var response = await getDefaultHttpClient().GetAsync(url)) {
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
        using (var response = await getDefaultHttpClient().PutAsync(url, content)) {
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
            return y != null && x != null && x.OriginalId== y.OriginalId && x.Slot == y.Slot && x.OwnerId == y.OwnerId;
        }

        public int GetHashCode(FileEntry obj) {
            return (obj.OriginalId+obj.OwnerId+$"slot={obj.Slot}").GetHashCode();
        }
    }

    private async Task<List<FileEntry>?> GetPeerList(bool retry=false) {
        var url = $"http://{peerAddress}/list?auth={authToken}&type=all&sync";
        bool needRetry = false;
        using (var response = await getDefaultHttpClient().GetAsync(url)) {
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
                    { new StringContent($"{entry.Slot}"), "Slot" },
                    { new StringContent(entry.OriginalId), "OriginalId" },
                    { new StringContent($"{entry.LastModifiedDate}"), "FileDate" },
                    { new StringContent($"{entry.CreationDate}"), "CreationDate" },
                    { new StringContent(entry.MetaInfo ?? ""), "MetaInfo" },
                    { new StringContent(entry.AttrDataJson), "ExtAttr" },
                    { new StringContent($"{entry.Duration}"), "Duration" },
                    { body, "File", entry.Name }
                };
                var url = $"http://{peerAddress}/{GetSlotId(entry.Slot)}/upload";
                using (var response = await getInfiniteHttpClient().PostAsync(url, content, ct)) {
                    return response.IsSuccessStatusCode;
                }
            }
        } catch(Exception e) {
            _logger.Error(e);
            return false;
        }
    }

    private const int BUFF_SIZE = 1 * 1024 * 1024;

    private string GetSlotId(int slot) {
        return $"slot{slot}/";
    }

    private async Task<bool> DownloadEntry(FileEntry entry, ProgressProc progress, CancellationToken ct) {
        string type = entry.Type == "mp4" ? "video" : "photo";
        string url = $"http://{peerAddress}/{type}?id={entry.Id}&auth={authToken}";
        progress(0, entry.Size);
        try {
            using (var response = await getInfiniteHttpClient().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                if (!response.IsSuccessStatusCode) return false;
                using (var content = response.Content)
                using (var inStream = await content.ReadAsStreamAsync())
                using (var entryCreator = await _secureStorageService.CreateEntry(entry.OwnerId, entry.Slot, entry.OriginalId, true)) {
                    if (entryCreator == null) { throw new InvalidOperationException("cannot create entry."); }
                    var total = content.Headers.ContentLength ?? 0L;
                    var buff = new byte[BUFF_SIZE];
                    var recv = 0L;
                    while (true) {
                        ct.ThrowIfCancellationRequested();
                        int len = await inStream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                        if (len == 0) {
                            entryCreator.Complete(entry.Name, entry.Size, entry.Type, entry.LastModifiedDate, entry.CreationDate, entry.Duration, entry.MetaInfo, entry);
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

    private async Task<bool> GetExtAttributes(FileEntry fromPeerEntry, FileEntry toMyEntry) {
        string url = $"http://{peerAddress}/extension?id={fromPeerEntry.Id}&auth={authToken}";
        try {
            using (var response = await getDefaultHttpClient().GetAsync(url)) {
                if (!response.IsSuccessStatusCode) return false;
                using (var content = response.Content) {
                    var jsonString = await content.ReadAsStringAsync();
                    var dic = ItemExtAttributes.FromJson(jsonString);
                    _databaseService.EditEntry(entries => {
                        var e = entries.GetById(toMyEntry.Id);
                        if (e == null) return false;
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
            return false;
        }
        catch (Exception e) {
            _logger.Error(e, "get attributes error");
            return false;
        }
    }

    private async Task<bool> PutExtAttributes(FileEntry fromMyEntry, FileEntry toPeerEntry) {
        string url = $"http://{peerAddress}/extension?id={toPeerEntry.Id}&auth={authToken}";
        try {
            var json = JsonConvert.SerializeObject(new Dictionary<string, object> {
                { "cmd", "extension" },
                { "id", toPeerEntry.Id },
                { "ownerId", toPeerEntry.OwnerId},
                { "slot", toPeerEntry.Slot },
                { "attrDate", fromMyEntry.ExtAttrDate },
                { "rating", fromMyEntry.Rating },
                { "mark", fromMyEntry.Mark },
                { "label", fromMyEntry.Label ?? "" },
                { "category", fromMyEntry.Category ?? "" },
                { "chapters", fromMyEntry.Chapters ?? "" },
            });
            using (var response = await getDefaultHttpClient().PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"))) {
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception e) {
            _logger.Error(e, "put attributes error");
            return false;
        }
    }

    private async Task<bool> SyncOwnerList() {
        try {
            var url = $"http://{peerAddress}/sync/owners";
            using (var response = await getDefaultHttpClient().PutAsync(url, new StringContent(_databaseService.OwnerList.JsonForSync(), Encoding.UTF8, "application/json"))) {
                if(!response.IsSuccessStatusCode) {
                    return false;
                }
                using (var content = response.Content) {
                    var jsonString = await content.ReadAsStringAsync();
                    _databaseService.EditOwnerList(ownerList=>{
                        return ownerList.SyncByJson(jsonString);
                    });
                }
            }
            return true;
        } catch(Exception e) {
            _logger.Error(e, "PutMyOwnerInfo error");
            return false;
        }
    }

    private async Task<IList<DeviceMigrationInfo>?> GetMigrationHistoryFromPeer() {
        string url = $"http://{peerAddress}/migration/history";
        try {
            using (var response = await getDefaultHttpClient().GetAsync(url)) {
                if (!response.IsSuccessStatusCode) return null;
                using (var content = response.Content) {
                    var jsonString = await content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                    if (json != null && json.ContainsKey("list")) {
                        var list = json["list"] as JArray;
                        if (list != null) {
                            return list.Select(it => {
                                return DeviceMigrationInfo.FromDictionary((JObject)it);
                            }).ToList();
                        }
                    }
                }
            }
            return new List<DeviceMigrationInfo>();
        }
        catch (Exception e) {
            _logger.Error(e, "get migration history error");
            return null;
        }
    }
    private async Task<bool> PutMigrationHistoryToPeer(IList<DeviceMigrationInfo> list) {
        if(list.Count == 0) {
            return true;
        }

        string url = $"http://{peerAddress}/migration/history";
        try {
            var json = JsonConvert.SerializeObject(new Dictionary<string, object> {
                { "cmd", "put/migration/history" },
                { "list", list.Select(it=>it.ToDictionary()) }
            });
            using (var response = await getDefaultHttpClient().PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"))) {
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception e) {
            _logger.Error(e, "put migration history error");
            return false;
        }
    }





    private struct Pair {
        public FileEntry peer;
        public FileEntry my;
        public bool IsDeleted => peer.IsDeleted || my.IsDeleted;
    }

    WeakReference<XamlRoot>? Parent=null;
    Regex regAddress = new Regex(@"(?:(?<ip>\d+\.\d+\.\d+\.\d+)|(?<name>[a-zA-Z]+\w*))(?::(?<port>\d+))?");

    public async Task<bool> Start(
        string peerAddress, 
        string peerPassword, 
        bool peerToLocalOnly,
        XamlRoot? parent, 
        ErrorMessageProc errorMessageProc,
        SyncStateProc syncTaskProc, 
        ProgressProc countProgress, 
        ProgressProc byteProgress) {
        try { 
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
                    if (!await RemoteAuth()) {
                        errorProc("Authentication Error.", false);
                        return false;
                    }

                    // 同期の前に無効レコード（実ファイルのないレコード）を削除しておく。
                    _databaseService.EditEntry(entries => {
                        var del = entries.Sweep();
                        _logger.Info($"{del} records are swept.");
                        return del > 0;
                    });

                    // OwnerInfoTableの同期
                    await SyncOwnerList();


                    // マイグレーション情報の同期
                    syncTaskProc(SyncTask.SyncMigrationFromPeer);
                    // Peer側のマイグレーション情報を取得
                    var history = await GetMigrationHistoryFromPeer();
                    if(history == null) {
                        errorProc("migration data error.", false);
                        return false;
                    }
                    // ローカルのマイグレーション情報を更新（ローカルにしか存在しないエントリを返してくる）
                    var historyToUpdate = _deviceMigrationService.ApplyHistoryFromPeerServer(history, countProgress);
                    if (historyToUpdate == null) {
                        errorProc("busy in migration.", false);
                        return false;
                    }
                    if (historyToUpdate.Count > 0) {
                        syncTaskProc(SyncTask.SyncMigrationToPeer);
                        _logger.Debug($"Migration history: {historyToUpdate.Count} entries to update peer.");
                        // ローカルにしかないエントリーをPeerに送信して、Peer側のマイグレーション情報を更新
                        if (!await PutMigrationHistoryToPeer(historyToUpdate)) {
                            errorProc("migration data update error.", false);
                            return false;
                        }
                    }

                    var myList = _databaseService.Entries.List(-1, false);
                    var peerList = await GetPeerList();
                    if (peerList == null) {
                        errorProc("No peer items.", false);
                        return false;
                    }
                    var comparator = new FileEntryComparator();
                    // ピア側にしか存在しないエントリのリスト
                    var peerNewFile = peerList.Except(myList, comparator).Where(it => !it.IsDeleted).ToList();
                    // ローカル側にしか存在しないエントリのリスト
                    var myNewFile = myList.Except(peerList, comparator).Where(it => !it.IsDeleted).ToList();
                    // ピア側とローカル側の両方に存在するエントリのマップ(name+ownerId+slot -> my-FileEntry)
                    string NameKey(FileEntry entry) {
                        return entry.Name + entry.OwnerId + $"s{entry.Slot}";
                    }
                    var myCommonFileDic = myList.Intersect(peerList, comparator).Aggregate(new Dictionary<string, FileEntry>(), (dic, entry) => {
                        try {
                            dic.Add(NameKey(entry), entry); return dic;
                        }
                        catch (Exception) {
                            // entry.Name に重複があると、このエラーになる。一時、SecureCameraの不具合で、Idがつけ変わってしまうケースがあって、これが発生した。
                            _logger.Error($"{entry.Name} {entry.OwnerId}(Slot={entry.Slot})");
                            var x = myList.Where(it => it.Name == entry.Name).ToList();
                            var y = peerList.Where(it => it.Name == entry.Name).ToList();
                            x.ForEach(it => _logger.Error($"{it.Id}"));
                            y.ForEach(it => _logger.Error($"{it.Id}"));
                            throw;
                        }
                    });
                    // ピア側で削除されていない、且つ、最終更新日時の異なる peer と my のペアのリスト
                    var commonPair = peerList.Intersect(myList, comparator).Select(it => new Pair { peer = it, my = myCommonFileDic[NameKey(it)] }).Where(it => !it.IsDeleted && it.peer.LastModifiedDate != it.my.LastModifiedDate).ToList();


                    // リモート側に新しく追加されたファイルをダウンロード
                    if (peerNewFile.Count > 0) {
                        syncTaskProc(SyncTask.DownloadNew);
                        int counter = 0;
                        foreach (var entry in peerNewFile) {
                            ct.ThrowIfCancellationRequested();
                            counter++;
                            _logger.Debug($"Download (NEW): [{counter}/{peerNewFile.Count}] {entry.Name}");
                            countProgress(counter, peerNewFile.Count);
                            await DownloadEntry(entry, byteProgress, ct);
                        }
                    }

                    // リモート側の更新日時が新しいものをダウンロードして上書き
                    var peerUpdates = commonPair.Where(it => it.peer.LastModifiedDate > it.my.LastModifiedDate).ToList();
                    if (peerUpdates.Count > 0) {
                        syncTaskProc(SyncTask.DownloadUpdate);
                        int counter = 0;
                        foreach (var pair in peerUpdates) {
                            ct.ThrowIfCancellationRequested();
                            counter++;
                            _logger.Debug($"Download (UPD):  [{counter}/{peerUpdates.Count}] {pair.my.Name}: {new DateTime(pair.my.LastModifiedDate)} <-- {new DateTime(pair.peer.LastModifiedDate)}");
                            countProgress(counter, peerUpdates.Count);
                            await DownloadEntry(pair.peer, byteProgress, ct);
                        }
                    }

                    // Peerへのアップロード
                    if (!peerToLocalOnly) {
                        // ローカル側に新しく追加されたファイルをアップロード
                        if (myNewFile.Count > 0) {
                            syncTaskProc(SyncTask.UploadingNew);
                            int counter = 0;
                            foreach (var entry in myNewFile) {
                                ct.ThrowIfCancellationRequested();
                                counter++;
                                _logger.Debug($"Upload (NEW): [{counter}/{myNewFile.Count}] {entry.Name}");
                                countProgress(counter, myNewFile.Count);
                                await UploadEntry(entry, byteProgress, ct);

                                // ToDo:
                                // ExtAttrs が反映されていない。
                            }
                        }

                        // ローカル側の更新日時が新しいものをアップロード
                        var myUpdates = commonPair.Where(it => it.peer.LastModifiedDate > it.my.LastModifiedDate).ToList();
                        if (myUpdates.Count > 0) {
                            syncTaskProc(SyncTask.UploadingUpdate);
                            int counter = 0;
                            foreach (var pair in myUpdates) {
                                ct.ThrowIfCancellationRequested();
                                counter++;
                                _logger.Debug($"Upload (UPD):  [{counter}/{myUpdates.Count}] {pair.my.Name}: {new DateTime(pair.my.LastModifiedDate)} --> {new DateTime(pair.peer.LastModifiedDate)}");
                                countProgress(counter, myUpdates.Count);
                                await UploadEntry(pair.my, byteProgress, ct);
                            }
                        }
                    }

                    // リモート側で削除されたファイルをローカルからも削除
                    var deletedFile = myList.Where(it => !it.IsDeleted).Intersect(peerList.Where(it => it.IsDeleted), comparator).ToList();
                    if (deletedFile.Count > 0) {
                        syncTaskProc(SyncTask.Deleting);
                        int counter = 0;
                        foreach (var entry in deletedFile) {
                            ct.ThrowIfCancellationRequested();
                            counter++;
                            _logger.Debug($"Delete:  [{counter}/{deletedFile.Count}] {entry.Name}");
                            countProgress(counter, deletedFile.Count);
                            await _secureStorageService.DeleteEntry(entry);
                        }
                    }

                    // 属性の同期
                    var attrUpdatedPairs = peerList.Intersect(myList, comparator).Select(it => new Pair { peer = it, my = myCommonFileDic[NameKey(it)] }).Where(it => !it.IsDeleted && it.peer.ExtAttrDate != it.my.ExtAttrDate).ToList();
                    if (attrUpdatedPairs.Count > 0) {
                        syncTaskProc(SyncTask.SyncAttributes);
                        int counter = 0;
                        foreach (var entry in attrUpdatedPairs) {
                            ct.ThrowIfCancellationRequested();
                            if (entry.peer.ExtAttrDate > entry.my.ExtAttrDate) {
                                // Peer側の方が新しい場合、Peer側の属性をローカルにコピー
                                _logger.Debug($"Download (ATTR): {entry.my.Name}: {new DateTime(entry.my.ExtAttrDate)} <-- {new DateTime(entry.peer.ExtAttrDate)}");
                                await GetExtAttributes(entry.peer, entry.my);
                            }
                            else if (entry.peer.ExtAttrDate < entry.my.ExtAttrDate) {
                                // ローカル側の方が新しい場合、ローカル側の属性をPeerにコピー
                                _logger.Debug($"Upload (ATTR): {entry.my.Name}: {new DateTime(entry.my.ExtAttrDate)} --> {new DateTime(entry.peer.ExtAttrDate)}");
                                await PutExtAttributes(entry.my, entry.peer);
                            }
                            countProgress(counter, attrUpdatedPairs.Count);
                        }
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
        finally {
            _defaultClient?.Dispose();
            _defaultClient = null;
            _infiniteClient?.Dispose();
            _infiniteClient = null;
        }
    }

    public void Cancel() {
        _cancellationTokenSource?.Cancel();
    }
}
