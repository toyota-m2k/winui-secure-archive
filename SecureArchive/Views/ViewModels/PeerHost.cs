using Newtonsoft.Json;
using SecureArchive.Utils.Server.mdns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels {
    internal class PeerHost {
        [JsonProperty("address")]
        public string Address { get; }
        [JsonProperty("service")]
        public string? ServiceName { get; }      // InstanceName
        [JsonProperty("hostname")]
        public string? Hostname { get; }
        [JsonProperty("https")]
        public bool IsHttps { get; }
        [JsonProperty("fingerprint")]
        public string? Fingerprint { get; }

        public string DisplayLabel {
            get {
                var scheme = IsHttps ? "HTTPS" : "HTTP";
                if (ServiceName != null) {
                    return $"{ServiceName} ({Address}) [{scheme}]";
                }
                else {
                    return $"{Address} [{scheme}]";
                }
            }
        }

        public override bool Equals(object? obj) {
            if (obj is PeerHost other) {
                return Address == other.Address
                    && IsHttps == other.IsHttps
                    && Fingerprint == other.Fingerprint
                    && ServiceName == other.ServiceName
                    && Hostname == other.Hostname;
            }
            return false;
        }

        public override int GetHashCode() {
            return $"{Address}|{IsHttps}|{Fingerprint}|{ServiceName}|{Hostname}".GetHashCode();
        }

        public string ToJson() {
            return JsonConvert.SerializeObject(this);
        }
        public static PeerHost? FromJson(string? json) {
            if (json == null) return null;
            return JsonConvert.DeserializeObject<PeerHost>(json);
        }

        public string Scheme => IsHttps ? "https" : "http";
        public string BaseUrl => $"{Scheme}://{Address}/";

        public string MakeUrl(string path) {
            return $"{BaseUrl}{path.TrimStart('/')}";
        }

        public PeerHost(string address, string? serviceName, string? hostname, bool isHttps, string? fingerprint) {
            Address = address;
            ServiceName = serviceName;
            Hostname = hostname;
            IsHttps = isHttps;
            Fingerprint = NormalizeFp(fingerprint);
        }
        public static PeerHost PairedHost(string address, string serverName, string hostname, bool isHttps, string? fingerprint) {
            return new PeerHost(address, serverName, hostname, isHttps, fingerprint);
        }
        public static PeerHost? DirectHost(string? address, bool isHttps) {
            if (string.IsNullOrEmpty(address)) return null;
            return new PeerHost(address, null, null, isHttps, null);
        }
        public static PeerHost FromDiscoveredPeer(DiscoveredPeer peer) {
            return new PeerHost(peer.HostAddress, peer.InstanceName, peer.Hostname, peer.IsHttps, peer.Fingerprint);
        }

        /**
         * SHA-256 指紋を "AB:CD:..." 形式のコロン区切り大文字 16 進に正規化する。
         * 文字列に不要文字 ("-", スペース等) が混ざっていても 16 進文字だけ抽出。
         * 64 桁 (32 バイト) ぴったりでないものは入力をそのまま大文字化して返す (比較は呼び出し側で行う)。
         */
        public static string NormalizeFp(string? fp) {
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
