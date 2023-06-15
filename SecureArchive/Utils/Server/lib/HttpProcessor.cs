// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using HttpContent = SecureArchive.Utils.Server.lib.model.HttpContent;

namespace SecureArchive.Utils.Server.lib;

public class HttpProcessor {

    #region Fields

    //private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

    private List<Route> Routes = new List<Route>();
    private UtLog Logger = new UtLog(typeof(HttpProcessor));

    #endregion

    #region Constructors

    public HttpProcessor() {
    }

    #endregion

    #region Public Methods
    public void HandleClient(TcpClient tcpClient) {
        Task.Run(() => {
            using (tcpClient)
            using (Stream inputStream = GetInputStream(tcpClient))
            using (Stream outputStream = GetOutputStream(tcpClient)) {
                Logger.Debug("Started.");
    
                IHttpResponse response = ProcessRequest(inputStream, outputStream);


                //// HttpRequest request = GetRequest(inputStream, outputStream);

                //// route and handle the request...
                ////var route = GetRoute(request, out var httpResponse);
                //if (route != null) {
                //    try {
                //        response = route.Process(request);
                //    } catch(Exception e) {
                //        Logger.LogError(e, "Route.Process");
                //    }
                //}
                //if(response == null) {
                //    response = HttpErrorResponse.InternalServerError(request);
                //}

                //Console.WriteLine("{0} {1}", response.ToString(), request.Url);
                try {
                    Logger.Info($"Sending: {response.Request?.Url ?? "?"}");
                    response.WriteResponse(outputStream);
                    outputStream.Flush();
                    Logger.Info($"Complete: {response.Request?.Url ?? "?"}");
                }
                catch (Exception e) {
                    Logger.Error(e, $"WriteResponse {response.Request?.Url ?? "?"}");
                }
            }
            Logger.Debug("Finished.");
        });
    }

    // this formats the HTTP response...

    public void AddRoute(Route route) {
        Routes.Add(route);
    }

    #endregion

    #region Private Methods

    private static string Readline(Stream stream) {
        int next_char;
        string data = "";
        while (true) {
            next_char = stream.ReadByte();
            if (next_char == '\n') { break; }
            if (next_char == '\r') { continue; }
            if (next_char == -1) { Thread.Sleep(1); continue; };
            data += Convert.ToChar(next_char);
        }
        return data;
    }

    private static void Write(Stream stream, string text) {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private Stream GetOutputStream(TcpClient tcpClient) {
        return tcpClient.GetStream();
    }

    private Stream GetInputStream(TcpClient tcpClient) {
        return tcpClient.GetStream();
    }


    //private IHttpResponse RouteRequest(Stream inputStream, Stream outputStream, HttpRequest request) {

    //    List<Route> routes = Routes.Where(x => Regex.Match(request.Url, x.UrlRegex).Success).ToList();

    //    if (!routes.Any()) {
    //        return HttpErrorResponse.NotFound(request);
    //    }

    //    Route? route = routes.FirstOrDefault(x => x.Method == request.Method);

    //    if (route == null) {
    //        return HttpErrorResponse.MethodNotAllowed(request);
    //    }

    //    // extract the path if there is one
    //    var match = Regex.Match(request.Url, route.UrlRegex);
    //    if (match.Groups.Count > 1) {
    //        request.Path = match.Groups[1].Value;
    //    }
    //    else {
    //        request.Path = request.Url;
    //    }

    //    // trigger the route handler...
    //    request.Route = route;
    //    try {
    //        return route.Process(request);
    //    }
    //    catch (Exception ex) {
    //        Logger.LogError(ex, "Processing Route.");
    //        return HttpErrorResponse.InternalServerError(request);
    //    }

    //}

    private string GetValueOrNull(Dictionary<string, string> map, string key, string def = "") {
        return map.TryGetValue(key, out var value) ? value : def;
    }

    private bool IsTextType(string contentType) {
        var type = contentType.ToLower();
        if (type.StartsWith("text/")) return true;
        if (type.StartsWith("application/json")) return true;
        else return false;
    }

    private IHttpResponse ProcessRequest(Stream inputStream, Stream outputStream) {
        try {
            var request = ParseHeader(inputStream, outputStream);
            Route route = GetRoute(request);
            ParseContent(inputStream, request, route);
            return route.Process(request);
        }
        catch (Exception ex) {
            Logger.Error(ex, "Route.Process");
            if (ex is HttpException httpException) {
                return httpException.ErrorResponse;
            }
            else {
                return HttpErrorResponse.InternalServerError(null);
            }
        }
    }


    private HttpRequest ParseHeader(Stream inputStream, Stream outputStream) {
        //Read Request Line
        string request = Readline(inputStream);

        string[] tokens = request.Split(' ');
        if (tokens.Length != 3) {
            throw new Exception("invalid http request line");
        }
        string method = tokens[0].ToUpper();
        string url = tokens[1];
        string protocolVersion = tokens[2];

        //Read Headers
        Dictionary<string, string> headers = new Dictionary<string, string>();
        string line;
        while ((line = Readline(inputStream)) != null) {
            if (line.Equals("")) {
                break;
            }

            int separator = line.IndexOf(':');
            if (separator == -1) {
                throw new Exception("invalid http header line: " + line);
            }
            string name = line.Substring(0, separator);
            int pos = separator + 1;
            while (pos < line.Length && line[pos] == ' ') {
                pos++;
            }

            string value = line.Substring(pos, line.Length - pos);
            headers.Add(name.ToLower(), value);
        }
        return new HttpRequest(method, url, headers, outputStream);
    }

    private Route GetRoute(HttpRequest request) {
        List<Route> routes = Routes.Where(x => Regex.Match(request.Url, x.UrlRegex).Success).ToList();

        if (!routes.Any()) {
            throw HttpErrorResponse.NotFound(request).Exception;
        }

        Route? route = routes.FirstOrDefault(x => x.Method == request.Method);

        if (route == null) {
            throw HttpErrorResponse.MethodNotAllowed(request).Exception;
        }

        // extract the path if there is one
        var match = Regex.Match(request.Url, route.UrlRegex);
        if (match.Groups.Count > 1) {
            request.Path = match.Groups[1].Value;
        }
        else {
            request.Path = request.Url;
        }
        return route;
    }



    private HttpRequest ParseContent(Stream inputStream, HttpRequest request, Route route) {
        model.HttpContent content;
        long contentLength = Convert.ToInt64(GetValueOrNull(request.Headers, "content-length", "0"));
        string contentType = GetValueOrNull(request.Headers, "content-type", "");
        //string contentEncoding = GetValueOrNull(request.Headers, "Content-Encoding", "");
        //string contentTransferEncoding = GetValueOrNull(request.Headers, "Content-Transfer-Encoding", "");

        if (route.ExpectTextBody && IsTextType(contentType) && 0 < contentLength) {
            int totalBytes = Convert.ToInt32(request.Headers["content-length"]);
            int bytesLeft = totalBytes;
            byte[] bytes = new byte[totalBytes];

            while (bytesLeft > 0) {
                byte[] buffer = new byte[bytesLeft > 1024 ? 1024 : bytesLeft];
                int n = inputStream.Read(buffer, 0, buffer.Length);
                buffer.CopyTo(bytes, totalBytes - bytesLeft);

                bytesLeft -= n;
            }
            // エンコーディングは UTF8 限定
            content = new HttpContent(Encoding.UTF8.GetString(bytes), contentType, contentLength);
        }
        else {
            content = new HttpContent(inputStream, contentType, contentLength);
        }
        request.Content = content;
        return request;
    }

    #endregion
}
