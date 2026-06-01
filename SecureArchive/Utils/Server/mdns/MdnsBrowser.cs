using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SecureArchive.Utils.Server.mdns {
    /// <summary>
    /// mDNS-SD クライアント (Discovery 側)。
    /// _booapi._tcp.local の PTR クエリを送信し、応答パケットから PTR / SRV / TXT / A を集約して
    /// <see cref="DiscoveredPeer"/> を <see cref="Peers"/> コレクションに公開する。
    ///
    /// 設計方針:
    ///   - UDP 5353 を各 NIC に bind して multicast group に join し、応答を待ち受ける。
    ///   - 1 パケットに PTR/SRV/TXT/A が同梱されるケース (MdnsAdvertiser がそう作る) と
    ///     別々に来るケースの両方を扱えるよう、InstanceFqdn をキーに <see cref="PeerBuilder"/> で集約する。
    ///   - UI スレッドへの marshal は Dispatcher.BeginInvoke で行う。
    /// </summary>
    public sealed class MdnsBrowser : IDisposable {
        private readonly UtLog logger = UtLog.Instance("MDNS.Brs");
        private readonly object _lock = new object();
        private readonly List<UdpClient> _sockets = new List<UdpClient>();
        private CancellationTokenSource? _cts = null;
        private string? _excludeFingerprint = null;
        //private readonly Dispatcher _ui;
        public event Action<UpdateInfo>? OnUpdate = null;

        // InstanceFqdn → 構築中ピア
        private readonly Dictionary<string, PeerBuilder> _building =
            new Dictionary<string, PeerBuilder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// UI バインド可能なピアコレクション。Dispatcher で更新される。
        /// 外部から渡すと、ViewModel 既存の ObservableCollection を再利用できる (XAML バインドが切れない)。
        /// </summary>
        private Dictionary<string,DiscoveredPeer> Peers { get; } = new();
        public class UpdateInfo {
            public enum UpdateType {
                AddOrUpdate,
                Remove,
            }
            public UpdateType Type;
            public DiscoveredPeer Peer;
            UpdateInfo(UpdateType type, DiscoveredPeer peer) {
                Type = type;
                Peer = peer;
            }
            public static UpdateInfo AddOrUpdate(DiscoveredPeer peer) => new UpdateInfo(UpdateType.AddOrUpdate, peer);
            public static UpdateInfo Remove(DiscoveredPeer peer) => new UpdateInfo(UpdateType.Remove, peer);
        }

        public MdnsBrowser(Action<UpdateInfo>? onUpdate = null) {
            OnUpdate += onUpdate;
        }

        /// <summary>
        /// Discovery 開始。socket bind + 受信ループ起動 + PTR クエリ送信を行う。
        /// </summary>
        /// <param name="excludeSelfFingerprint">
        /// 自インスタンスの証明書 SHA-256 指紋。HTTPS で同じ指紋を持つピアは <see cref="Peers"/> に出さない。
        /// HTTP のみ動作中、もしくは自身が listener でない場合は null。
        /// </param>
        public void Start(string? excludeSelfFingerprint = null) {
            lock (_lock) {
                if (_cts != null) return;
                _excludeFingerprint = NormalizeFp(excludeSelfFingerprint);
                _cts = new CancellationTokenSource();
                BindSockets();
            }

            SendPtrQuery();
            // RFC 6762 風に初回は少しずらしてもう一度送る
            Task.Run(async () => {
                try { await Task.Delay(800, _cts.Token); SendPtrQuery(); } catch { }
            });
        }

        /// <summary>
        /// PTR クエリの再送信。socket は閉じない (UI の「Rescan」ボタン用)。
        /// </summary>
        public void Rescan() {
            if (_cts == null) return;
            SendPtrQuery();
        }

        public void Dispose() {
            OnUpdate = null;
            CancellationTokenSource? cts;
            List<UdpClient> sockets;
            lock (_lock) {
                cts = _cts;
                if (cts == null) return;
                _cts = null;
                sockets = new List<UdpClient>(_sockets);
                _sockets.Clear();
                _building.Clear();
            }
            try { cts.Cancel(); } catch { }
            foreach (var s in sockets) {
                try { s.Close(); } catch { }
            }
        }

        // ---- Sockets / IO -----------------------------------------------------------------

        private void BindSockets() {
            foreach (var addr in MdnsCommon.MyAddresses) {
                UdpClient? sock = null;
                try {
                    sock = new UdpClient(AddressFamily.InterNetwork);
                    sock.ExclusiveAddressUse = false;
                    sock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    sock.Client.Bind(new IPEndPoint(addr, MdnsCommon.MdnsPort));
                    sock.JoinMulticastGroup(MdnsCommon.MulticastV4, addr);
                    // Browser は自分が出すクエリの応答を見る必要がない (応答は外部から来る) ので loopback off で OK
                    sock.MulticastLoopback = false;
                    _sockets.Add(sock);
                    logger.Debug($"mDNS-B bound on {addr}");
                    var s = sock;
                    Task.Run(() => ListenLoop(s, _cts!.Token));
                } catch (Exception e) {
                    logger.Error(e, $"mDNS-B bind on {addr} failed: {e.Message}");
                    try { sock?.Close(); } catch { }
                }
            }
        }

        private async Task ListenLoop(UdpClient sock, CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                UdpReceiveResult r;
                try {
                    r = await sock.ReceiveAsync().ConfigureAwait(false);
                } catch (ObjectDisposedException) {
                    return;
                } catch (Exception e) {
                    if (ct.IsCancellationRequested) return;
                    logger.Error(e, $"mDNS-B receive: {e.Message}");
                    continue;
                }
                try {
                    ParseResponse(r.Buffer);
                } catch (Exception e) {
                    logger.Error(e, $"mDNS-B parse: {e.Message}");
                }
            }
        }

        private void SendPtrQuery() {
            byte[] pkt = BuildPtrQuery();
            List<UdpClient> socks;
            lock (_lock) socks = new List<UdpClient>(_sockets);
            foreach (var s in socks) {
                try {
                    s.Send(pkt, pkt.Length, new IPEndPoint(MdnsCommon.MulticastV4, MdnsCommon.MdnsPort));
                } catch (Exception e) {
                    logger.Error(e, $"mDNS-B PTR send: {e.Message}");
                }
            }
        }

        private static byte[] BuildPtrQuery() {
            var ms = new MemoryStream();
            MdnsCommon.WriteUInt16(ms, 0);          // ID
            MdnsCommon.WriteUInt16(ms, 0x0000);     // Flags: QR=0
            MdnsCommon.WriteUInt16(ms, 1);          // QDCOUNT
            MdnsCommon.WriteUInt16(ms, 0);          // ANCOUNT
            MdnsCommon.WriteUInt16(ms, 0);          // NSCOUNT
            MdnsCommon.WriteUInt16(ms, 0);          // ARCOUNT
            MdnsCommon.WriteName(ms, MdnsCommon.ServiceType + ".local");
            MdnsCommon.WriteUInt16(ms, 12);         // QTYPE = PTR
            MdnsCommon.WriteUInt16(ms, 0x0001);     // QCLASS = IN
            return ms.ToArray();
        }

        // ---- Response parsing -------------------------------------------------------------

        private void ParseResponse(byte[] buf) {
            if (buf.Length < 12) return;
            int flags = (buf[2] << 8) | buf[3];
            if ((flags & 0x8000) == 0) return; // QR=0 → クエリ。Browser は応答だけ取る
            int qd = (buf[4] << 8) | buf[5];
            int an = (buf[6] << 8) | buf[7];
            int ns = (buf[8] << 8) | buf[9];
            int ar = (buf[10] << 8) | buf[11];

            int off = 12;
            // QD セクション (普通は 0 だが念のため skip)
            for (int i = 0; i < qd; i++) {
                if (!MdnsCommon.TryReadName(buf, ref off, out _)) return;
                if (off + 4 > buf.Length) return;
                off += 4;
            }

            string? ptrInstance = null;
            string? srvTarget = null;
            ushort srvPort = 0;
            uint srvTtl = 0;
            string? srvOwner = null;     // SRV を所有するインスタンス名 (= InstanceFqdn)
            Dictionary<string, string>? txt = null;
            string? txtOwner = null;
            uint txtTtl = 0;
            // A レコードは hostname → IP のマップ
            var aMap = new Dictionary<string, List<IPAddress>>(StringComparer.OrdinalIgnoreCase);

            int total = an + ns + ar;
            for (int i = 0; i < total; i++) {
                if (!MdnsCommon.TryReadName(buf, ref off, out var name)) return;
                if (off + 10 > buf.Length) return;
                ushort type = MdnsCommon.ReadUInt16(buf, ref off);
                off += 2; // CLASS (cache-flush bit を含む) は無視
                uint ttl = MdnsCommon.ReadUInt32(buf, ref off);
                ushort rdlen = MdnsCommon.ReadUInt16(buf, ref off);
                int rdEnd = off + rdlen;
                if (rdEnd > buf.Length) return;

                string? lower = name?.ToLowerInvariant() ?? "";
                switch (type) {
                    case 12: { // PTR
                        if (lower == MdnsCommon.ServiceType + ".local.") {
                            int inner = off;
                            if (MdnsCommon.TryReadName(buf, ref inner, out var inst)) {
                                ptrInstance = inst;
                            }
                        }
                        break;
                    }
                    case 33: { // SRV
                        if (lower.EndsWith("." + MdnsCommon.ServiceType + ".local.", StringComparison.OrdinalIgnoreCase)) {
                            int inner = off;
                            inner += 2; // priority
                            inner += 2; // weight
                            ushort port = MdnsCommon.ReadUInt16(buf, ref inner);
                            if (MdnsCommon.TryReadName(buf, ref inner, out var target)) {
                                srvOwner = name;
                                srvTarget = target;
                                srvPort = port;
                                srvTtl = ttl;
                            }
                        }
                        break;
                    }
                    case 16: { // TXT
                        if (lower.EndsWith("." + MdnsCommon.ServiceType + ".local.", StringComparison.OrdinalIgnoreCase)) {
                            txtOwner = name;
                            txt = ParseTxt(buf, off, rdlen);
                            txtTtl = ttl;
                        }
                        break;
                    }
                    case 1: { // A
                        if (rdlen == 4) {
                            var ipBytes = new byte[4];
                            Array.Copy(buf, off, ipBytes, 0, 4);
                            if (!aMap.TryGetValue(name!, out var list)) {
                                list = new List<IPAddress>();
                                aMap[name!] = list;
                            }
                            list.Add(new IPAddress(ipBytes));
                        }
                        break;
                    }
                    // AAAA (28) はスコープ外
                }
                off = rdEnd;
            }

            // 集約キー: 持っているうち最初に見つかった InstanceFqdn
            string? instanceKey = srvOwner ?? txtOwner ?? ptrInstance;
            if (instanceKey == null) return;

            // SRV/TXT どちらかが TTL=0 (goodbye) なら Peers から消す
            if ((srvOwner != null && srvTtl == 0) || (txtOwner != null && txt != null && txtTtl == 0)) {
                RemovePeer(instanceKey);
                return;
            }

            UpdatePeer(instanceKey, srvTarget, srvPort, txt, aMap);
        }

        private static Dictionary<string, string> ParseTxt(byte[] buf, int off, int len) {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int end = off + len;
            while (off < end) {
                int n = buf[off++];
                if (n == 0 || off + n > end) continue;
                var s = Encoding.UTF8.GetString(buf, off, n);
                off += n;
                int eq = s.IndexOf('=');
                if (eq < 0) d[s] = "";
                else d[s.Substring(0, eq)] = s.Substring(eq + 1);
            }
            return d;
        }

        // ---- Aggregation ------------------------------------------------------------------

        private sealed class PeerBuilder {
            public string? InstanceFqdn;
            public string? SrvTarget;        // 例: "MachineA-BooTube.local."
            public ushort SrvPort;
            public Dictionary<string, string>? Txt;
            public HashSet<IPAddress> Ips = new HashSet<IPAddress>();

            public bool IsComplete =>
                SrvPort > 0 && Txt != null && (Ips.Count > 0 || HasTxtHostname);

            public bool HasTxtHostname =>
                Txt != null && Txt.TryGetValue("hostname", out var v) && !string.IsNullOrEmpty(v);

            public string BestHostname {
                get {
                    if (!string.IsNullOrEmpty(SrvTarget)) return SrvTarget;
                    if (Txt != null && Txt.TryGetValue("hostname", out var v)) return v;
                    return "";
                }
            }
        }

        private void UpdatePeer(string instanceFqdn, string? srvTarget, ushort srvPort,
                                Dictionary<string, string>? txt, Dictionary<string, List<IPAddress>> aMap) {
            PeerBuilder? b = null;
            lock (_lock) {
                if (!_building.TryGetValue(instanceFqdn, out b)) {
                    b = new PeerBuilder { InstanceFqdn = instanceFqdn };
                    _building[instanceFqdn] = b;
                }
                if (!string.IsNullOrEmpty(srvTarget)) b.SrvTarget = srvTarget;
                if (srvPort > 0) b.SrvPort = srvPort;
                if (txt != null) b.Txt = txt;
                // A レコードを SRV target に紐付けて追加
                if (aMap.Count > 0) {
                    foreach (var kv in aMap) {
                        if (string.Equals(kv.Key, b.SrvTarget, StringComparison.OrdinalIgnoreCase)) {
                            foreach (var ip in kv.Value) b.Ips.Add(ip);
                        }
                    }
                    // SRV が未確定でも A が来ているなら、とりあえず全部足しておく (後で SRV が確定したら整合する)
                    if (string.IsNullOrEmpty(b.SrvTarget)) {
                        foreach (var kv in aMap) foreach (var ip in kv.Value) b.Ips.Add(ip);
                    }
                }
            }
            TryPublish(b);
        }

        private void TryPublish(PeerBuilder b) {
            if (!b.IsComplete) return;
            var txt = b.Txt;
            string? app = txt?.GetValue("app");
            if (app == null || !string.Equals(app, MdnsCommon.AppId, StringComparison.OrdinalIgnoreCase)) {
                // BooTube 以外 (winui-secure-archive 等) は除外
                return;
            }
            bool isHttps = txt?.GetValue("https") == "1";
            string fp = txt?.GetValue("fp") ?? "";
            int ver = txt?.GetValue("version") != null && int.TryParse(txt.GetValue("version"), out var iv) ? iv : 1;

            // 自インスタンス除外 (指紋一致のみ。HTTP 動作中は除外不能だがそれは想定通り)
            if (!string.IsNullOrEmpty(_excludeFingerprint) &&
                string.Equals(NormalizeFp(fp), _excludeFingerprint, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var label = InstanceLabelFromFqdn(b.InstanceFqdn!);
            var addrList = b.Ips.ToList();
            var peer = new DiscoveredPeer(
                label,
                b.BestHostname,
                addrList,
                b.SrvPort, isHttps, fp,
                app ?? "", ver,
                (txt != null) ? new Dictionary<string, string>(txt, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                DateTime.UtcNow);
            Peers[peer.InstanceName.ToLower()] = peer;
            OnUpdate?.Invoke(UpdateInfo.AddOrUpdate(peer));
        }

        private void RemovePeer(string instanceFqdn) {
            lock (_lock) {
                _building.Remove(instanceFqdn);
            }
            var label = InstanceLabelFromFqdn(instanceFqdn);
            if (label==null) return;

            var removed = Peers.GetValue(label.ToLower());
            if (removed != null) {
                OnUpdate?.Invoke(UpdateInfo.Remove(removed));
            }
        }

        //private void DispatchToUi(Action action) {
        //    if (_ui.CheckAccess()) {
        //        action();
        //    } else {
        //        _ui.BeginInvoke(action);
        //    }
        //}

        private static string InstanceLabelFromFqdn(string fqdn) {
            // "MachineA-BooTube._booapi._tcp.local." → "MachineA-BooTube"
            if (string.IsNullOrEmpty(fqdn)) return "";
            int dot = fqdn.IndexOf('.');
            return dot > 0 ? fqdn.Substring(0, dot) : fqdn;
        }

        private static string NormalizeFp(string? fp) {
            if (string.IsNullOrWhiteSpace(fp)) return "";
            var sb = new StringBuilder(fp.Length);
            foreach (var c in fp) {
                if (Uri.IsHexDigit(c)) sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }
}
