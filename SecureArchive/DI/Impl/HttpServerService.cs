using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Server.lib;
using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI.Impl;

internal class HttpServerService {
    private ILogger _logger;
    private HttpServer _server = null!;
    private IUserSettingsService _userSettingsService;
    public HttpServerService(ILoggerFactory factory, IUserSettingsService userSettingsService) {
        _logger = factory.CreateLogger<HttpServerService>();
        _userSettingsService = userSettingsService;
    }

    private List<Route> Routes() {
        return new List<Route> {
            Route.single(
                name: "nop",
                regex: "",
                method: "GET",
                process: (HttpRequest request) => {
                    return new TextHttpResponse(request, HttpStatusCode.Ok, "nothing to do.");
                }),
            Route.multi(
                name: "upload",
                regex: "",
                process: (HttpRequest request) => {


                    return new TextHttpResponse(request, HttpStatusCode.Ok, "Done.");
                }),
        };
    }


    private async void Initialize() {
        _server = new HttpServer(await _userSettingsService.GetAsync<int>(SettingsKey.PortNo), routes, _logger);
    }
}
