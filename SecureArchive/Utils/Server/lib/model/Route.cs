// Copyright (C) 2016 by Barend Erasmus and donated to the public domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SecureArchive.Utils.Server.lib.response;
using static SecureArchive.Utils.Server.lib.model.HttpContent;

namespace SecureArchive.Utils.Server.lib.model;

interface IRoute {
    string Name { get; } // descriptive name for debugging
    string UrlRegex { get; }
    string Method { get; }
    bool ExpectTextBody { get; }
    Func<HttpRequest, IHttpResponse> Process { get; }
}

public class Route : IRoute { 
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
    public static Route generic(string name, string regex, string method, bool expectTextBody, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name,regex, method, expectTextBody, process);
    }
    public static Route post(string name, string regex, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, "POST", false, process);
    }
    public static Route get(string name, string regex, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, "GET", true, process);
    }
    public static Route put(string name, string regex, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, "PUT", true, process);
    }
    public static Route delete(string name, string regex, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, "DELETE", true, process);
    }

    public static Route text(string name, string regex, string method, Func<HttpRequest, IHttpResponse> process) {
        return new Route(name, regex, method, true, process);
    }
}

