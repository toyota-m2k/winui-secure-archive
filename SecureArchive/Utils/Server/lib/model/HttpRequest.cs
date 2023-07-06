// Copyright (C) 2016 by Barend Erasmus and donated to the public domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecureArchive.Utils.Server.lib.model;

public class HttpRequest
{
    public int Id { get; } = GenerateId();
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


    static int IdGenerator = 0;
    static int GenerateId() {
        return Interlocked.Increment(ref IdGenerator);
    }
}
