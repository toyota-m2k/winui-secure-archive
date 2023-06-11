// Copyright (C) 2016 by Barend Erasmus and donated to the public domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SecureArchive.Utils.Server.lib.response;

namespace SecureArchive.Utils.Server.lib.model;

public class Route
{
    public string Name { get; } // descriptive name for debugging
    public string UrlRegex { get; }
    public string Method { get; }
    public bool ExpectTextBody { get; }
    public Func<HttpRequest, IHttpResponse> Process { get; }

    public Route(string name, string urlRegex, string method, bool expectTextBody, Func<HttpRequest, IHttpResponse> process)
    {
        Name = name;
        UrlRegex = urlRegex;
        Method = method;
        ExpectTextBody = expectTextBody;
        Process = process;
    }
    public static Route of(string name, string regex, string method, bool expectTextBody, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name,regex, method, expectTextBody, process);
    }
    public static Route multi(string name, string regex, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, "POST", false, process);
    }
    public static Route single(string name, string regex, string method, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, method, true, process);
    }
}
