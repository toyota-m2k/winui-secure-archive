// Copyright (C) 2016 by Barend Erasmus and donated to the public domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecureArchive.Utils.Server.lib.model;

public class HttpRequest
{
    public string Method { get; }
    public string Url { get; }
    public Dictionary<string, string> Headers { get; }

    // 以下は HttpProcessor#RouteRequest()でセットされる。
    public HttpContent? Content { get; set; } = null;
    public Route? Route { get; set; } = null;
    public string? Path { get; set; } = null;
    public Stream OutputStream { get; }

    public HttpRequest(string method, string url, Dictionary<string, string>? headers, Stream outputStream)
    {
        Method = method;
        Url = url;
        Headers = headers ?? new Dictionary<string, string>();
        OutputStream = outputStream;
    }

    //public override string ToString() {
    //    if (!string.IsNullOrWhiteSpace(Content))
    //        if (!Headers.ContainsKey("Content-Length"))
    //            Headers.Add("Content-Length", Content.Length.ToString());

    //    return string.Format("{0} {1} HTTP/1.0\r\n{2}\r\n\r\n{3}", Method, Url, string.Join("\r\n", Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))), Content);
    //}
}
