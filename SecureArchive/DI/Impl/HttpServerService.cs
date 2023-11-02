using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Utils.Crypto;
using SecureArchive.Utils.Server.lib;
using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Navigation;
using Windows.Security.Cryptography;
using static SecureArchive.Utils.SeekableInputStream;
using static SecureArchive.Utils.Server.lib.model.HttpContent;
using HttpContent = SecureArchive.Utils.Server.lib.model.HttpContent;

namespace SecureArchive.DI.Impl;

internal class HttpServerService : IHttpServreService {
    private ILogger _logger;
    private HttpServer _server;
    private ISecureStorageService _secureStorageService;
    private IDatabaseService _databaseService;
    private IPasswordService _passwordService;
    private IBackupService _backupService;
    private CryptoStreamHandler _cryptoStreamHandler;

    //private IUserSettingsService _userSettingsService;
    public HttpServerService(
        ILoggerFactory factory, 
        ISecureStorageService secureStorageService,
        IDatabaseService databaseService,
        IPasswordService passwordService,
        IBackupService backupService) {
        _logger = factory.CreateLogger<HttpServerService>();
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
        _passwordService = passwordService;
        _backupService = backupService;

        _server = new HttpServer(Routes(), _logger);

        _cryptoStreamHandler = new CryptoStreamHandler(_secureStorageService, _logger);
    }

    #region Uploading

    //private Dictionary<string, UploadHandler> _uploadingTasks = new Dictionary<string, UploadHandler>();
    //private void RegisterUploadTask(UploadHandler handler) {
    //    lock(_uploadingTasks) { _uploadingTasks.Add(handler.ID, handler); }
    //}
    //private void UnregisterUploadTask(UploadHandler handler) {
    //    lock (_uploadingTasks) { _uploadingTasks.Remove(handler.ID); }
    //}
    //private UploadHandler? LookupUploadTask(string id) {
    //    lock(_uploadingTasks) {
    //        if(_uploadingTasks.TryGetValue(id, out var handler)) return handler;
    //        return null;
    //    }
    //}

    class UploadHandler : IMultipartContentHander, IDisposable {
        private static AtomicInteger idGenerator = new AtomicInteger();
        private static string newId() => Convert.ToString(idGenerator.IncrementAndGet());

        public long ReceivedLength { get; private set; } = 0;
        public long ContentLength { get; private set; } = 0;

        private ISecureStorageService _secureStorageService;
        public string ID { get; }
        public UploadHandler(ISecureStorageService secureStorageService) {
            ID = newId();
            _secureStorageService = secureStorageService;
        }
        public IDictionary<string, string> Parameters { get; } = new Dictionary<string, string>();

        private IEntryCreator? _entryCreator = null;
        public bool HasError { get; set; }

        public Stream CreateFile(HttpContent multipartBody) {
            Dispose();
            ContentLength = 0;
            ReceivedLength = 0;

            if (!Parameters.TryGetValue("OwnerId", out var ownerId)) {
                ownerId = "*";
            }
            if (!Parameters.TryGetValue("OriginalId", out var originalId)) {
                originalId = "";
            }
            var entryCreator = _secureStorageService.CreateEntry(ownerId, originalId, true).Result;
            if(entryCreator==null) {
                throw new InvalidOperationException("cannot create entry.");
            }
            _entryCreator = entryCreator;
            return entryCreator.OutputStream;
        }

        public void CloseFile(HttpContent multipartBody, Stream outStream) {
            if(_entryCreator==null) {
                return;
            }
            var name = string.IsNullOrEmpty(multipartBody.Filename) ? multipartBody.Name : multipartBody.Filename;
            long originalDate = 0;
            if (Parameters.TryGetValue("FileDate", out var fileDateText)) {
                originalDate = Convert.ToInt64(fileDateText);
            }
            long creationDate = 0;
            if (Parameters.TryGetValue("CreationDate", out var creationDateText)) {
                creationDate = Convert.ToInt64(creationDateText);
            }

            if (!Parameters.TryGetValue("MetaInfo", out var metaInfo)) {
                metaInfo = null;
            }
            _entryCreator?.Complete(name, multipartBody.ContentLength, Path.GetExtension(name), originalDate, creationDate, metaInfo);
            Dispose();
        }

        public void Dispose() {
            if (_entryCreator != null) {
                _entryCreator.Dispose();
                _entryCreator = null;
            }
        }

        public void Progress(long current, long total) {
            ReceivedLength = current;
            ContentLength = total;
        }
    }

    class CryptoStreamHandler {
        private FileEntry? _currentEntry = null;
        private SeekableInputStream? _seekableInputStream = null;
        private Mutex _mutex = new Mutex();
        private ISecureStorageService _secureStorageService;
        private ILogger _logger;
        
        public CryptoStreamHandler(ISecureStorageService secureStorageService, ILogger logger) {
            _secureStorageService = secureStorageService;
            _logger = logger;
        }

        private bool _locked = false;
        private long _tick = 0L;
        public Stream LockStream(FileEntry entry) {
            if(_locked) {
                _seekableInputStream?.Dispose();
                _seekableInputStream = null;
            }
            _mutex.WaitOne();
            _locked = true;
            _tick = System.Environment.TickCount64;
            _logger.Debug($"Locked: [Entry={entry.Id}]");
            if (_seekableInputStream != null) {
                if (_currentEntry?.Id == entry.Id) {
                    return _seekableInputStream;
                } else {
                    _seekableInputStream.Dispose();
                }
            }
            _currentEntry = entry;
            _seekableInputStream = new SeekableInputStream(_secureStorageService.OpenEntry(entry), reopenStreamProc: (oldStream) => {
                oldStream.Dispose();
                return _secureStorageService.OpenEntry(entry);
            });
            return _seekableInputStream;
        }

        public void UnlockStream(FileEntry entry) {
            if (_currentEntry?.Id == entry.Id) {
                _logger.Debug($"Unlocked: [Entry={entry.Id}] ({(System.Environment.TickCount64-_tick)/1000} sec)");
                _locked = false;
                _mutex.ReleaseMutex();
            } else {
                _logger.Debug($"Cannot Unlock: Entry Mismatch: {_currentEntry?.Id} - {entry.Id}");
            }
        }
    }

    //class CryptoStreamHandlerMap {
    //    private Dictionary<string, CryptoStreamHandler> _map = new ();

    //    public CryptoStreamHandler(string clientId) {
    //        if(_map.ContainsKey(clientId)) {
    //            return _map[clientId];
    //        }
    //    }
    //}

    #endregion
    #region Authentication

    class OneTimePasscode {
        IPasswordService _passwordService;
        readonly TimeSpan ValidTerm = new TimeSpan(0, 30, 0);    // 30分
        string _challenge = null!; // = Guid.NewGuid().ToString();
        string _authToken = null!; // = CryptographicBuffer.EncodeToHexString(RandomNumberGenerator.GetBytes(8).AsBuffer());
        DateTime _tick = DateTime.MinValue;

        public OneTimePasscode(IPasswordService passwordService) {
            _passwordService = passwordService;
            Reset();
        }

        private void Validate() {
            if(DateTime.Now - _tick > ValidTerm) {
                Reset();
            }
        }

        public string Challenge {
            get {
                Validate();
                return _challenge;
            }
        }

        void Reset() {
            _challenge = Guid.NewGuid().ToString();
            _authToken = CryptographicBuffer.EncodeToHexString(RandomNumberGenerator.GetBytes(8).AsBuffer());
            _tick = DateTime.Now;
        }

        public async Task<IHttpResponse> Authenticate(HttpRequest request, string? authPhrase) {
            Validate();
            if(!await _passwordService.CheckRemoteKey(_challenge, authPhrase)) {
                return UnauthorizedResponse(request);
            } else {
                // 認証が成功した時点を有効期限の起点とする。
                _tick = DateTime.Now;

                var dic = new Dictionary<string, object> {
                    { "cmd", "auth" },
                    { "token", _authToken },
                    { "term", (_tick + ValidTerm).Ticks }
                };
                return TextHttpResponse.FromJson(request, dic);
            }
        }

        public bool CheckAuthToken(string? token) {
            Validate();
            if (token == null) return false;
            if (token != _authToken) return false;

            // 認証が成功した時点を有効期限の起点とする。
            // つまり、連続した要求は、常に成功するが、一定時間放置すると無効になる。
            _tick = DateTime.Now;
            return true;
        }

        public IHttpResponse UnauthorizedResponse(HttpRequest request) {
            Validate();
            var dic = new Dictionary<string, object> { { "challenge", _challenge } };
            return TextHttpResponse.FromJson(request, dic, HttpStatusCode.Unauthorized);
        }
    }
    #endregion
    #region Router

    public Regex RegRange = new Regex(@"bytes=(?<start>\d+)(?:-(?<end>\d+))?");
    public Regex RegUploading = new Regex(@"/uploading/(?<hid>[^?/]+)(?:[?].*)*");

    private List<Route> Routes() {
        var oneTimePasscode = new OneTimePasscode(_passwordService);

        return new List<Route> {
            Route.get(
                name: "nop",
                regex: "/nop",
                process: (HttpRequest request) => {
                    return new TextHttpResponse(request, HttpStatusCode.Ok, "nothing to do.");
                }),
            Route.post(
                name: "upload",
                regex: "/upload",
                process: (HttpRequest request) => {
                    var content = request.Content;
                    if(content==null) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    //var p = QueryParser.Parse(request.Url);
                    using(var handler = new UploadHandler(_secureStorageService)) {
                        //RegisterUploadTask(handler);
                        //// Uploadされたファイルの登録に時間がかかるので、一旦、202応答を返しておく。
                        //// 登録処理の経過が知りたければ、GET /uploading/<taskId> で取得する。
                        //var response = new TextHttpResponse(request, HttpStatusCode.Accepted, "");
                        //response.Headers.Add("Location", $"/uploading/{handler.ID}");
                        //response.WriteResponse(request.OutputStream);
                        //_logger.Debug("Accept file : 202 Response.");

                        //// そのあとで登録処理を実行
                        try {
                            _logger.Debug("Parsing Multipart");
                            content.ParseMultipartContent(handler);
                            //UnregisterUploadTask(handler);
                            _logger.Debug("Parsing Multipart ... Completed");
                            return new TextHttpResponse(request, HttpStatusCode.Ok, "Done.");
                        } catch (Exception ex) {
                            _logger.Error(ex, "Parsing multipart body error.");
                            handler.HasError = true;
                            return HttpErrorResponse.InternalServerError(request);
                        }
                        // 処理完了
                    }
                    //// すでに応答を返しているので、ここは制御を戻すだけ。
                    //return NullResponse.Get(request);
                }),
            //Route.get(
            //    name: "upload task",
            //    regex: @"/uploading/\w+",
            //    process: (HttpRequest request) => {
            //        var m = RegUploading.Match(request.Url);
            //        if(!m.Success) {
            //            return HttpErrorResponse.BadRequest(request);
            //        }
            //        var handlerId = m.Groups["hid"].Value;
            //        if(handlerId.IsEmpty()) {
            //            return HttpErrorResponse.BadRequest(request);
            //        }

            //        var handler = LookupUploadTask(handlerId);
            //        if(handler==null) {
            //            var p = QueryParser.Parse(request.Url);
            //            var oid = p.GetValue("o");
            //            var cid = p.GetValue("c");
            //            if(oid.IsNotEmpty() && cid.IsNotEmpty() && _secureStorageService.IsRegistered(oid,cid)) {
            //                return new TextHttpResponse(request, HttpStatusCode.Ok, "Done.");
            //            } else {
            //                return HttpErrorResponse.NotFound(request);
            //            }
            //        } else {
            //            var dic = new Dictionary<string, object> {
            //                { "current", $"{handler.ReceivedLength}" },
            //                { "total", $"{handler.ContentLength}" },
            //            };
            //            return TextHttpResponse.FromJson(request, dic, HttpStatusCode.Accepted);
            //        }
            //    }),
            Route.get(
                name: "capability",
                regex:@"/capability",
                process: (HttpRequest request) => {
                    var cap = new Dictionary<string,object> {
                        {"cmd", "capability"},
                        {"serverName", "SecureArchive"},
                        {"version", 1},
                        {"root", "/" },
                        {"category", false},
                        {"rating", false},
                        {"mark", false},
                        {"chapter", false },
                        {"sync", false },
                        {"acceptRequest", false},
                        {"backup", true},
                        {"hasView", false},
                        {"authentication", true},
                        {"challenge",  oneTimePasscode.Challenge },
                    };
                    return TextHttpResponse.FromJson(request, cap);
                }),
            Route.put(
                name: "authentication",
                regex: "/auth",
                process: (HttpRequest request) => {
                    var passPhrease = request.Content?.TextContent?.Trim();
                    return oneTimePasscode.Authenticate(request, passPhrease).Result;
                }),
            Route.get(
                name: "auth & nop",
                regex: @"/auth/.*",
                process: (HttpRequest request) => {
                    if(!oneTimePasscode.CheckAuthToken(request.Url.Substring(6))) {
                        return oneTimePasscode.UnauthorizedResponse(request);
                    }
                    return new TextHttpResponse(request, HttpStatusCode.Ok, "Ok");
                }),
            Route.get(
                name: "list",
                regex: @"/list(?:\?.*)?",
                process: (HttpRequest request) => {
                    var p = QueryParser.Parse(request.Url);
                    if(!oneTimePasscode.CheckAuthToken(p.GetValue("auth"))) {
                        return oneTimePasscode.UnauthorizedResponse(request);
                    }
                    var sync = p.GetValue("sync")?.ToLower() == "true";
                    var type = p.GetValue("type")?.ToLower() ?? "";
                    var list = _databaseService.Entries.List(
                        predicate: (it) => {
                            switch(type) {
                                case "all": return true;
                                case "photo": return it.Type == "jpg" || it.Type == "png";
                                default: return it.Type == "mp4";
                            }
                        }, 
                        select: (entry) => {
                            if(sync) {
                                return entry.ToDictionary();
                            } else {
                                return new Dictionary<string, object>() {
                                    { "id", entry.Id },
                                    { "name", entry.Name },
                                    { "type", entry.Type },
                                    { "size", entry.Size },
                                };
                            }
                        }
                    );
                    var dic = new Dictionary<string,object> {
                        {"cmd", "list" },
                        {"date", DateTime.UtcNow.ToFileTimeUtc() },
                        {"list", list },
                    };
                    return TextHttpResponse.FromJson(request, dic);
                }),
            Route.get(
                name: "video",
                regex: @"/video\?\w+",
                process: (HttpRequest request) => {
                    var p = QueryParser.Parse(request.Url);
                    if(!oneTimePasscode.CheckAuthToken(p.GetValue("auth"))) {
                        return oneTimePasscode.UnauthorizedResponse(request);
                    }
                    var id = Convert.ToInt64(p.GetValue("id", "0"));
                    var oid = p.GetValue("o");
                    var cid = p.GetValue("c");

                    FileEntry? entry = null;
                    if(oid.IsNotEmpty() && cid.IsNotEmpty()) {
                        entry = _databaseService.Entries.GetByOriginalId(oid, cid);
                    } else {
                        entry = _databaseService.Entries.GetById(id);
                    }
                    if(entry==null) {
                        return HttpErrorResponse.NotFound(request);
                    }

                    if(!request.Headers.TryGetValue("range", out var range)) {
                        //Source?.StandardOutput($"BooServer: cmd=video({id})");
                        _logger.Debug("No-Ranged Request.");
                        return StreamingHttpResponse.CreateForRangedInitial(request, "video/mp4", _cryptoStreamHandler.LockStream(entry), entry.Size, ()=>_cryptoStreamHandler.UnlockStream(entry));
                    }

                    var match = RegRange.Match(range);
                    var ms = match.Groups["start"];
                    var me = match.Groups["end"];
                    var start = ms.Success ? Convert.ToInt64(ms.Value) : 0L;
                    var end = me.Success ? Convert.ToInt64(me.Value) : 0L;
                    if(start<0 || end<0 || (end>0 && start>end)) {
                        _logger.Error($"Hah? Start={start} End={end}");
                    }


                    _logger.Debug($"Ranged Request. {start} - {end}");
                    return StreamingHttpResponse.CreateForRanged(request, "video/mp4", _cryptoStreamHandler.LockStream(entry), start, end, entry.Size, ()=>_cryptoStreamHandler.UnlockStream(entry));
                }),
            Route.get(
                name: "photo",
                regex: @"/photo\?\w+",
                process: (HttpRequest request) => {
                    var p = QueryParser.Parse(request.Url);
                    if(!oneTimePasscode.CheckAuthToken(p.GetValue("auth"))) {
                        return oneTimePasscode.UnauthorizedResponse(request);
                    }
                    var id = Convert.ToInt64(p.GetValue("id", "0"));
                    var oid = p.GetValue("o");
                    var cid = p.GetValue("c");

                    FileEntry? entry = null;
                    if(oid.IsNotEmpty() && cid.IsNotEmpty()) {
                        entry = _databaseService.Entries.GetByOriginalId(oid, cid);
                    } else {
                        entry = _databaseService.Entries.GetById(id);
                    }
                    if(entry==null) {
                        return HttpErrorResponse.NotFound(request);
                    }
                    return StreamingHttpResponse.CreateForNoRanged(request, "image/jpeg", _cryptoStreamHandler.LockStream(entry), entry.Size, ()=>_cryptoStreamHandler.UnlockStream(entry));
                }),
            Route.get(
                name: "chapters",
                regex: @"/chapter\?\w+",
                process: (request) => {
                    var id = QueryParser.Parse(request.Url)["id"];
                    var dic = new Dictionary<string, object>(){
                        { "cmd", "chapter"},
                        { "id", id },
                        { "chapters", new List<object>() }
                    };
                    return TextHttpResponse.FromJson(request, dic);
                }),
            Route.put(
                name:"register owner",
                regex: @"/owner",
                process: (request) => {
                    var content = request.Content?.TextContent;
                    if (content== null) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    if(!RegisterOwner(JsonConvert.DeserializeObject<Dictionary<string, string>>(content))) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    return TextHttpResponse.FromJson(request, new Dictionary<string,object>{ { "cmd", "owner" }, {"status", "registered"} });
                }),
            Route.put(
                name: "backup start",
                regex: @"/backup/request",
                process: (request) => {
                    var content = request.Content?.TextContent;
                    if (content== null) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    if (dic == null) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    var token = dic.GetValue("token");
                    var address = dic.GetValue("address");
                    var ownerId = dic.GetValue("id");
                    if(token.IsEmpty() ||address.IsEmpty() ||ownerId.IsEmpty()) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    RegisterOwner(dic);
                    if(!_backupService.Request(ownerId!, token!, address!)) {
                        return HttpErrorResponse.Conflict(request);
                    }
                    return TextHttpResponse.FromJson(request, new Dictionary<string,object>{ { "cmd", "backup" }, {"status", "accepted"} });
                }),
                
            
        };
    }

    private bool RegisterOwner(IDictionary<string,string>? dic) {
        if (dic == null) return false;
        var ownerId = dic.GetValue("id");
        var ownerName = dic.GetValue("name");
        var ownerType = dic.GetValue("type") ?? "*";
        var flag = Convert.ToInt32(dic.GetValue("flag") ?? "0");
        var option = dic.GetValue("option");
        if (ownerId.IsEmpty()|| ownerName.IsEmpty()) {
            return false;
        }
        _databaseService.EditOwnerList(list => {
            list.AddOrUpdate(ownerId, ownerName, ownerType, flag, option);
            return true;
        });
        return true;
    }

    #endregion
    #region Server

    public IObservable<bool> Running => _server.Running;
    
    public bool Start(int port) {
        //var port = await _userSettingsService.GetAsync<int>(SettingsKey.PortNo);
        return _server.Start(port);
    }

    public void Stop() {
        _server.Stop();
    }
    #endregion
}
