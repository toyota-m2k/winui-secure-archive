using System;
using System.Linq;
using System.Text;

namespace SecureArchive.Utils.Server.mdns {
    /// <summary>
    /// 同期相手 (別 BooTube インスタンス) の接続情報をひとまとめにする不変オブジェクト。
    /// </summary>
    public sealed class PeerEndpoint {
        public string Host { get; }                     // "192.168.0.153" or "host.local"
        public int Port { get; }
        public bool UseHttps { get; }
        public string ExpectedFingerprint { get; }      // HTTPS の場合は必須 (空なら検証失敗)

        public string Scheme => UseHttps ? "https" : "http";
        public string Authority => $"{Host}:{Port}";
        public string BaseUrl => $"{Scheme}://{Authority}/ytplayer";

        public PeerEndpoint(string host, int port, bool useHttps, string expectedFingerprint) {
            Host = host?.Trim() ?? "";
            Port = port;
            UseHttps = useHttps;
            ExpectedFingerprint = NormalizeFp(expectedFingerprint);
        }

        /// <summary>
        /// ユーザの "host[:port]" 形式入力を解釈する。port が含まれていれば優先、無ければ defaultPort。
        /// </summary>
        public static PeerEndpoint FromUserInput(string hostInput, int defaultPort, bool useHttps, string fp) {
            string h = (hostInput ?? "").Trim();
            int port = defaultPort;
            // IPv6 表記 ([::1]:port) はスコープ外。最後の ":" でホストとポートを分割。
            int colon = h.LastIndexOf(':');
            if (colon > 0 && int.TryParse(h.Substring(colon + 1), out var p)) {
                port = p;
                h = h.Substring(0, colon);
            }
            return new PeerEndpoint(h, port, useHttps, fp);
        }

        /// <summary>
        /// SHA-256 指紋を "AB:CD:..." 形式のコロン区切り大文字 16 進に正規化する。
        /// 文字列に不要文字 ("-", スペース等) が混ざっていても 16 進文字だけ抽出。
        /// 64 桁 (32 バイト) ぴったりでないものは入力をそのまま大文字化して返す (比較は呼び出し側で行う)。
        /// </summary>
        public static string NormalizeFp(string fp) {
            if (string.IsNullOrWhiteSpace(fp)) return "";
            var hex = new string(fp.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 64) return hex;
            var sb = new StringBuilder(95);
            for (int i = 0; i < 64; i += 2) {
                if (i > 0) sb.Append(':');
                sb.Append(hex[i]).Append(hex[i + 1]);
            }
            return sb.ToString();
        }
    }
}
