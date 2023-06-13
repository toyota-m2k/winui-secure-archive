/**
 * URLからクエリパラメータを辞書として取り出す
 * 
 * Copyright (c) 2021 @toyota-m2k.
 */
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SecureArchive.Utils.Server.lib;

public static class QueryParser {
    //private static Regex RegexQueryFragment = new Regex(@"(?:[?&](?<fragment>\w+(?:=\w+)?))+");
    //private static Regex RegexQueryNV = new Regex(@"(?<name>\w+)(?:=(?<value>\w+))?");
    private static Regex RegexQuery = new Regex(@"(?:[?&](?<name>\w+)(?:=(?<value>[^&=\r\n \t]+))?)");

    public static Dictionary<string, string> Parse(string uriString) {
        var r = new Dictionary<string, string>();
        var c = RegexQuery.Matches(uriString);
        for (int i = c.Count - 1; i >= 0; i--) {
            var m = c[i];
            if (m.Success) {
                var name = m.Groups["name"];
                if (name.Success) {
                    var value = m.Groups["value"];
                    if (value.Success) {
                        r[name.Value] = value.Value ?? "";
                    }
                    else {
                        r[name.Value] = "true";
                    }
                }
            }
        }
        return r;
    }
}
