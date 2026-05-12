using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SecureArchive.Utils.Server {
    /// <summary>
    /// mDNS-SD で検出したピアの情報。SyncDialog で表示しユーザに選ばせる。
    /// </summary>
    public sealed class DiscoveredPeer {
        public string InstanceName { get; }     // 例: "MachineA-BooTube"
        public string Hostname { get; }         // 例: "MachineA-BooTube.local." (SRV target、無ければ TXT hostname)
        public IReadOnlyList<IPAddress> Addresses { get; }
        public int Port { get; }
        public bool IsHttps { get; }            // TXT https=1
        public string Fingerprint { get; }      // TXT fp=  (HTTP の場合は空文字)
        public string AppId { get; }            // TXT app=bootube
        public int Version { get; }             // TXT version=2
        public IReadOnlyDictionary<string, string> RawTxt { get; }
        public DateTime LastSeen { get; }

        public DiscoveredPeer(
                string instanceName, string hostname,
                IReadOnlyList<IPAddress> addresses, int port,
                bool isHttps, string fingerprint,
                string appId, int version,
                IReadOnlyDictionary<string, string> rawTxt,
                DateTime lastSeen) {
            InstanceName = instanceName;
            Hostname = hostname;
            Addresses = addresses ?? new List<IPAddress>();
            Port = port;
            IsHttps = isHttps;
            Fingerprint = fingerprint ?? "";
            AppId = appId ?? "";
            Version = version;
            RawTxt = rawTxt ?? new Dictionary<string, string>();
            LastSeen = lastSeen;
        }

        /// <summary>
        /// UI の ListBox 表示用ラベル。例: "MachineA-BooTube (192.168.0.153:3501) [HTTPS]"
        /// </summary>
        public string DisplayLabel {
            get {
                string addr = Addresses.Count > 0
                    ? Addresses[0].ToString()
                    : (Hostname ?? "").TrimEnd('.');
                string s = $"{InstanceName} ({addr}:{Port})";
                if (IsHttps) s += " [HTTPS]";
                return s;
            }
        }

        /// <summary>
        /// SyncManager 用の PeerEndpoint を組み立てる。
        /// .local 名は Bonjour 未インストール環境で解決できないので IP を優先する。
        /// </summary>
        public PeerEndpoint ToEndpoint() {
            string host = Addresses.Count > 0
                ? Addresses[0].ToString()
                : (Hostname ?? "").TrimEnd('.');
            return new PeerEndpoint(host, Port, IsHttps, Fingerprint);
        }
    }
}
