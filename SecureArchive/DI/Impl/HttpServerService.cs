using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Utils.Server.lib;
using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;
using System.Text.RegularExpressions;
using static SecureArchive.Utils.Server.lib.model.HttpContent;
using HttpContent = SecureArchive.Utils.Server.lib.model.HttpContent;

namespace SecureArchive.DI.Impl;

internal class HttpServerService : IHttpServreService {
    private ILogger _logger;
    private HttpServer _server;
    private ISecureStorageService _secureStorageService;
    private IDatabaseService _databaseService;

    //private IUserSettingsService _userSettingsService;
    public HttpServerService(ILoggerFactory factory, ISecureStorageService secureStorageService, IDatabaseService databaseService) {
        _logger = factory.CreateLogger<HttpServerService>();
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;

        //_userSettingsService = userSettingsService;
        _server = new HttpServer(Routes(), _logger);
    }

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

        public Stream CreateFile(HttpContent multipartBody) {
            Dispose();
            ContentLength = 0;
            ReceivedLength = 0;
            var entryCreator = _secureStorageService.CreateEntry().Result;
            _entryCreator = entryCreator;
            return entryCreator.OutputStream;
        }

        public void CloseFile(HttpContent multipartBody, Stream outStream) {
            if(_entryCreator==null) {
                return;
            }
            var name = string.IsNullOrEmpty(multipartBody.Filename) ? multipartBody.Name : multipartBody.Filename;
            if (!Parameters.TryGetValue("OwnerId", out var ownerId)) {
                ownerId = "*";
            }
            long originalDate = 0;
            if (!Parameters.TryGetValue("FileDate", out var fileDateText)) {
                originalDate = Convert.ToInt64(fileDateText);
            }
            if (!Parameters.TryGetValue("OriginalId", out var originalId)) {
                originalId = "";
            }
            if (!Parameters.TryGetValue("MetaInfo", out var metaInfo)) {
                metaInfo = null;
            }
            _entryCreator?.Complete(ownerId, name, multipartBody.ContentLength, Path.GetExtension(name), originalDate, originalId, metaInfo);
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

    public Regex RegRange = new Regex(@"bytes=(?<start>\d+)(?:-(?<end>\d+))?");

    private List<Route> Routes() {
        FileEntry? currentEntry = null;
        SeekableInputStream? seekableInputStream = null;
        var challenge = Guid.NewGuid().ToString();
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
                        var response = new TextHttpResponse(request, HttpStatusCode.Accepted, "");
                        response.Headers.Add("Location", $"/uploading/{handler.ID}");
                        response.WriteResponse(request.OutputStream);
                        content.ParseMultipartContent(handler);
                        UnregisterUploadTask(handler);
                    }
                    return NullResponse.Instance;
                    //using(var handler = new UploadHandler(_secureStorageService)) {
                    //    content.ParseMultipartContent(handler);
                    //}
                    //return new TextHttpResponse(request, HttpStatusCode.Ok, "Done.");
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
                        {"category", false},
                        {"rating", false},
                        {"mark", false},
                        {"acceptRequest", false},
                        {"hasView", false},
                        {"authentication", true},
                        {"challenge",  challenge},
                    };
                    return TextHttpResponse.FromJson(request, cap);
                }),
            Route.get(
                name: "list",
                regex: @"/list(?:\?.*)?",
                process: (HttpRequest request) => {
                    var list = _databaseService.Entries.List()
                    .Where((it) => {
                        return it.Type == ".mp4";
                    }).Select((entry) => {
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
                    var id = Convert.ToInt64(QueryParser.Parse(request.Url)["id"]);
                    var entry = _databaseService.Entries.List().Where((it) => it.Id == id).SingleOrDefault();
                    if(entry==null) {
                        return HttpErrorResponse.NotFound(request);
                    }

                    if(currentEntry?.Id != entry.Id) {
                        seekableInputStream?.Dispose();
                        seekableInputStream = new SeekableInputStream(_secureStorageService.OpenEntry(entry), (oldStream) => {
                            oldStream.Dispose();
                            return _secureStorageService.OpenEntry(entry);
                        });
                    }
                    if(!request.Headers.TryGetValue("Range", out var range)) {
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
        };
    }

    public IObservable<bool> Running => _server.Running;
    
    public bool Start(int port) {
        //var port = await _userSettingsService.GetAsync<int>(SettingsKey.PortNo);
        return _server.Start(port);
    }

    public void Stop() {
        _server.Stop();
    }
}
