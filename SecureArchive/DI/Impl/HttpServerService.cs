﻿using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
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
    }

    #region Uploading

    private Dictionary<string, UploadHandler> _uploadingTasks = new Dictionary<string, UploadHandler>();
    private void RegisterUploadTask(UploadHandler handler) {
        lock(_uploadingTasks) { _uploadingTasks.Add(handler.ID, handler); }
    }
    private void UnregisterUploadTask(UploadHandler handler) {
        lock (_uploadingTasks) { _uploadingTasks.Remove(handler.ID); }
    }
    private UploadHandler? LookupUploadTask(string id) {
        lock(_uploadingTasks) {
            if(_uploadingTasks.TryGetValue(id, out var handler)) return handler;
            return null;
        }
    }

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
            if (!Parameters.TryGetValue("FileDate", out var fileDateText)) {
                originalDate = Convert.ToInt64(fileDateText);
            }
            if (!Parameters.TryGetValue("MetaInfo", out var metaInfo)) {
                metaInfo = null;
            }
            _entryCreator?.Complete(name, multipartBody.ContentLength, Path.GetExtension(name), originalDate, metaInfo);
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
            return token == _authToken;
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

    private List<Route> Routes() {
        FileEntry? currentEntry = null;
        SeekableInputStream? seekableInputStream = null;
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
                    using(var handler = new UploadHandler(_secureStorageService)) {
                        RegisterUploadTask(handler);
                        // Uploadされたファイルの登録に時間がかかるので、一旦、202応答を返しておく。
                        // 登録処理の経過が知りたければ、GET /uploading/<taskId> で取得する。
                        var response = new TextHttpResponse(request, HttpStatusCode.Accepted, "");
                        response.Headers.Add("Location", $"/uploading/{handler.ID}");
                        response.WriteResponse(request.OutputStream);
                        _logger.Debug("Accept file : 202 Response.");

                        // そのあとで登録処理を実行
                        try {
                            _logger.Debug("Parsing Multipart");
                            content.ParseMultipartContent(handler);
                            UnregisterUploadTask(handler);
                            _logger.Debug("Parsing Multipart ... Completed");
                        } catch (Exception ex) {
                            _logger.Error(ex, "Parsing multipart body error.");
                            handler.HasError = true;
                        }
                        // 処理完了
                    }
                    // すでに応答を返しているので、ここは制御を戻すだけ。
                    return NullResponse.Get(request);
                }),
            Route.get(
                name: "upload task",
                regex: @"/uploading/\w+",
                process: (HttpRequest request) => {
                    var id = request.Path?.Substring("/uploading/".Length);
                    if(id==null) {
                        return HttpErrorResponse.BadRequest(request);
                    }
                    var handler = LookupUploadTask(id);
                    if(handler==null) {
                        return new TextHttpResponse(request, HttpStatusCode.Ok, "Done.");
                    } else {
                        var dic = new Dictionary<string, object> {
                            { "current", $"{handler.ReceivedLength}" },
                            { "total", $"{handler.ContentLength}" },
                        };
                        return TextHttpResponse.FromJson(request, dic, HttpStatusCode.Accepted);
                    }
                }),
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
                    var list = _databaseService.Entries.List(
                        predicate: (it) => {
                            return it.Type == ".mp4";
                        }, 
                        select: (entry) => {
                            return new Dictionary<string, object>() {
                                { "id", entry.Id },
                                { "name", entry.Name },
                                { "type", entry.Type.Substring(1) },
                                { "size", entry.Size },
                        };
                    });
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
                    var entry = _databaseService.Entries.GetById(id);
                    if(entry==null) {
                        return HttpErrorResponse.NotFound(request);
                    }

                    if(currentEntry?.Id != entry.Id) {
                        currentEntry = entry;
                        seekableInputStream?.Dispose();
                        seekableInputStream = new SeekableInputStream(_secureStorageService.OpenEntry(entry), (oldStream) => {
                            oldStream.Dispose();
                            return _secureStorageService.OpenEntry(entry);
                        });
                    }
                    if(!request.Headers.TryGetValue("range", out var range)) {
                        //Source?.StandardOutput($"BooServer: cmd=video({id})");
                        return new StreamingHttpResponse(request, "video/mp4", seekableInputStream!, 0, 0);
                    }

                    var match = RegRange.Match(range);
                    var ms = match.Groups["start"];
                    var me = match.Groups["end"];
                    var start = ms.Success ? Convert.ToInt64(ms.Value) : 0;
                    var end = me.Success ? Convert.ToInt64(me.Value) : 0;

                    return new StreamingHttpResponse(request, "video/mp4", seekableInputStream!, start, end);
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
                    if(!_backupService.Backup(ownerId!, token!, address!)) {
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
            var reg = list.Get(ownerId!);
            if (reg != null) {
                reg.Option = option;
                reg.Name = ownerName!;
                reg.Type = ownerType;
                reg.Flags = flag;
            } else { 
                list.Add(ownerId!, ownerName!, ownerType, flag, option);
            }
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
