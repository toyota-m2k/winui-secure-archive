using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server;

/// <summary>
/// 最小限の mDNS-SD レスポンダ。
///
/// SecureArchive が _booapi._tcp.local サービスを LAN に広告する目的に特化していて、汎用 mDNS の
/// 全機能（recursion / cache coherency / unicast response 切替 等）は実装していない。
/// 主要動作:
///   - UDP 5353 / 224.0.0.251 で待ち受け、PTR/SRV/TXT/A 質問に応答する
///   - 起動直後に gratuitous announcement、その後 30 秒ごとに再広告
///   - Dispose 時に TTL=0 の goodbye パケットを送出
/// 参考: RFC 6762 (mDNS), RFC 6763 (DNS-SD)
///
/// _booapi._tcp.local は BooTube 系互換 REST API を提供するアプリで共通のサービスタイプ。
/// アプリ識別は TXT app=archive で行う (BooTube は app=bootube)。
/// </summary>
public class MdnsAdvertiser : IDisposable {
    public const string ServiceType = "_booapi._tcp";
    /// <summary>このアプリ識別子 (TXT app=...)。</summary>
    public const string AppId = "SA";
    private const int MdnsPort = 5353;
    private const uint TtlAnnouncement = 120;
    private const uint TtlHostAddress = 120;
    private const int AnnounceIntervalMs = 30000;
    private static readonly IPAddress MulticastV4 = IPAddress.Parse("224.0.0.251");

    private readonly UtLog _logger = UtLog.Instance(typeof(MdnsAdvertiser));

    private readonly object _lock = new object();
    private readonly List<UdpClient> _sockets = new List<UdpClient>();
    private CancellationTokenSource? _cts;

    private string _instance = "";
    private string _hostLocal = "";
    private ushort _port;
    private List<string> _txt = new List<string>();
    private List<IPAddress> _addresses = new List<IPAddress>();

    public bool IsRunning {
        get { lock (_lock) return _cts != null; }
    }

    /**
     * @param instanceName 「設定」で登録された Server Name
     */
    public void Start(string instanceName, int port, bool isHttps, string? fingerprint) {
        lock (_lock) {
            if (_cts != null) return;

            _instance = SanitizeInstanceName(instanceName);        // Settings.ServiceName
            _hostLocal = SanitizeHostname(Environment.MachineName) + ".local";
            _port = (ushort)port;
            _txt = new List<string> {
                "version=2",
                isHttps ? "https=1" : "https=0",
                "app=" + AppId,                     // "SA"
                "hostname=" + _hostLocal,           // "machine-name.local"
            };
            if (!string.IsNullOrEmpty(fingerprint)) {
                _txt.Add("fp=" + fingerprint);
            }
            _addresses = GetLocalIPv4Addresses();

            _cts = new CancellationTokenSource();
            BindSockets();
        }

        // 初回広告 (RFC 6762 8.3 に倣い少しずらして 2 回送る)
        SendUnsolicitedAnnouncement();
        Task.Run(async () => {
            try { await Task.Delay(800, _cts!.Token); SendUnsolicitedAnnouncement(); } catch { }
        });

        // 周期再広告ループ
        Task.Run(() => AnnounceLoop(_cts!.Token));
    }

    public void Dispose() {
        CancellationTokenSource? cts;
        List<UdpClient> sockets;
        lock (_lock) {
            cts = _cts;
            if (cts == null) return;
            _cts = null;
            sockets = new List<UdpClient>(_sockets);
            _sockets.Clear();
        }

        try { SendGoodbye(sockets); } catch (Exception e) { _logger.Error(e, "mDNS goodbye"); }

        try { cts.Cancel(); } catch { }
        foreach (var s in sockets) {
            try { s.Close(); } catch { }
        }
    }

    // ---- Sockets ----------------------------------------------------------------------

    private void BindSockets() {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!nic.SupportsMulticast) continue;
            var ipProps = nic.GetIPProperties();
            foreach (var ua in ipProps.UnicastAddresses) {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;
                UdpClient? sock = null;
                try {
                    sock = new UdpClient(AddressFamily.InterNetwork);
                    sock.ExclusiveAddressUse = false;
                    sock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    sock.Client.Bind(new IPEndPoint(ua.Address, MdnsPort));
                    sock.JoinMulticastGroup(MulticastV4, ua.Address);
                    sock.MulticastLoopback = false;
                    _sockets.Add(sock);
                    _logger.Debug($"mDNS bound on {ua.Address}");
                    var s = sock;
                    Task.Run(() => ListenLoop(s, _cts!.Token));
                }
                catch (Exception e) {
                    _logger.Error($"mDNS bind on {ua.Address} failed: {e.Message}");
                    try { sock?.Close(); } catch { }
                }
            }
        }
    }

    private async Task ListenLoop(UdpClient sock, CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            UdpReceiveResult result;
            try {
                result = await sock.ReceiveAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException) {
                return;
            }
            catch (Exception e) {
                if (ct.IsCancellationRequested) return;
                _logger.Error($"mDNS receive: {e.Message}");
                continue;
            }
            try {
                HandleQuery(result.Buffer, sock);
            }
            catch (Exception e) {
                _logger.Error($"mDNS handle: {e.Message}");
            }
        }
    }

    private async Task AnnounceLoop(CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                await Task.Delay(AnnounceIntervalMs, ct).ConfigureAwait(false);
                SendUnsolicitedAnnouncement();
            }
        }
        catch (TaskCanceledException) { }
    }

    // ---- Query handling ---------------------------------------------------------------

    private void HandleQuery(byte[] buffer, UdpClient sock) {
        if (buffer.Length < 12) return;
        int flags = (buffer[2] << 8) | buffer[3];
        if ((flags & 0x8000) != 0) return; // QR=1 → response、無視
        int qdcount = (buffer[4] << 8) | buffer[5];

        int offset = 12;
        bool needRespond = false;
        for (int i = 0; i < qdcount; i++) {
            string? qname;
            if (!TryReadName(buffer, ref offset, out qname)) return;
            if (offset + 4 > buffer.Length) return;
            int qtype = (buffer[offset] << 8) | buffer[offset + 1];
            offset += 4; // type + class

            string lower = qname!.ToLowerInvariant();
            if (qtype == 12 && lower == ServiceType + ".local.") needRespond = true;
            else if (qtype == 12 && lower == "_services._dns-sd._udp.local.") needRespond = true;
            else if (lower == InstanceFqdn().ToLowerInvariant() && (qtype == 33 || qtype == 16 || qtype == 255)) needRespond = true;
            else if (lower == HostFqdn().ToLowerInvariant() && (qtype == 1 || qtype == 255)) needRespond = true;
        }

        if (needRespond) {
            var packet = BuildResponsePacket(includePTR: true, ttlOverride: null);
            SendOnSocket(sock, packet);
        }
    }

    // ---- Sending ---------------------------------------------------------------------

    private void SendUnsolicitedAnnouncement() {
        var packet = BuildResponsePacket(includePTR: true, ttlOverride: null);
        List<UdpClient> sockets;
        lock (_lock) sockets = new List<UdpClient>(_sockets);
        foreach (var s in sockets) SendOnSocket(s, packet);
    }

    private void SendGoodbye(List<UdpClient> sockets) {
        var packet = BuildResponsePacket(includePTR: true, ttlOverride: 0);
        foreach (var s in sockets) SendOnSocket(s, packet);
    }

    private void SendOnSocket(UdpClient sock, byte[] packet) {
        try {
            sock.Send(packet, packet.Length, new IPEndPoint(MulticastV4, MdnsPort));
        }
        catch (ObjectDisposedException) { }
        catch (Exception e) {
            _logger.Error($"mDNS send: {e.Message}");
        }
    }

    // ---- DNS message construction ----------------------------------------------------

    // 末尾ドット付きで DNS パケット側の表記と合わせる。
    private string InstanceFqdn() => _instance + "." + ServiceType + ".local.";
    private string HostFqdn() => _hostLocal + ".";

    private byte[] BuildResponsePacket(bool includePTR, uint? ttlOverride) {
        var ms = new MemoryStream();
        WriteUInt16(ms, 0);
        WriteUInt16(ms, 0x8400); // QR=1, AA=1
        WriteUInt16(ms, 0);      // QDCOUNT
        int answerCount = (includePTR ? 1 : 0) + 1 /*SRV*/ + 1 /*TXT*/ + _addresses.Count;
        WriteUInt16(ms, (ushort)answerCount);
        WriteUInt16(ms, 0); // NSCOUNT
        WriteUInt16(ms, 0); // ARCOUNT

        uint ttl = ttlOverride ?? TtlAnnouncement;

        if (includePTR) {
            WriteRecord(ms, ServiceType + ".local", 12 /*PTR*/, 0x0001, ttl, () => {
                WriteName(ms, InstanceFqdn());
            });
        }
        WriteRecord(ms, InstanceFqdn(), 33 /*SRV*/, 0x8001, ttl, () => {
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, _port);
            WriteName(ms, HostFqdn());
        });
        WriteRecord(ms, InstanceFqdn(), 16 /*TXT*/, 0x8001, ttl, () => {
            if (_txt.Count == 0) {
                ms.WriteByte(0);
            }
            else {
                foreach (var kv in _txt) {
                    var bytes = Encoding.UTF8.GetBytes(kv);
                    if (bytes.Length > 255) continue;
                    ms.WriteByte((byte)bytes.Length);
                    ms.Write(bytes, 0, bytes.Length);
                }
            }
        });
        foreach (var ip in _addresses) {
            WriteRecord(ms, HostFqdn(), 1 /*A*/, 0x8001, TtlHostAddress, () => {
                var bytes = ip.GetAddressBytes();
                ms.Write(bytes, 0, bytes.Length);
            });
        }

        return ms.ToArray();
    }

    private static void WriteRecord(MemoryStream ms, string name, ushort type, ushort cls, uint ttl, Action writeData) {
        WriteName(ms, name);
        WriteUInt16(ms, type);
        WriteUInt16(ms, cls);
        WriteUInt32(ms, ttl);
        long lenPos = ms.Position;
        WriteUInt16(ms, 0);
        long startPos = ms.Position;
        writeData();
        long endPos = ms.Position;
        int rdlen = (int)(endPos - startPos);
        ms.Position = lenPos;
        WriteUInt16(ms, (ushort)rdlen);
        ms.Position = endPos;
    }

    private static void WriteName(MemoryStream ms, string fqdn) {
        foreach (var label in fqdn.Split('.')) {
            if (string.IsNullOrEmpty(label)) continue;
            var bytes = Encoding.UTF8.GetBytes(label);
            if (bytes.Length > 63) {
                Array.Resize(ref bytes, 63);
            }
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
        }
        ms.WriteByte(0);
    }

    private static void WriteUInt16(MemoryStream ms, ushort v) {
        ms.WriteByte((byte)(v >> 8));
        ms.WriteByte((byte)(v & 0xFF));
    }

    private static void WriteUInt32(MemoryStream ms, uint v) {
        ms.WriteByte((byte)((v >> 24) & 0xFF));
        ms.WriteByte((byte)((v >> 16) & 0xFF));
        ms.WriteByte((byte)((v >> 8) & 0xFF));
        ms.WriteByte((byte)(v & 0xFF));
    }

    // ---- DNS name parsing -------------------------------------------------------------

    private static bool TryReadName(byte[] buffer, ref int offset, out string? name) {
        var sb = new StringBuilder();
        int cursor = offset;
        int? returnOffset = null;
        int hops = 0;
        while (true) {
            if (cursor >= buffer.Length) { name = null; return false; }
            int len = buffer[cursor];
            if (len == 0) {
                cursor++;
                if (returnOffset.HasValue) offset = returnOffset.Value;
                else offset = cursor;
                name = sb.ToString() + (sb.Length > 0 ? "." : "");
                return true;
            }
            if ((len & 0xC0) == 0xC0) {
                if (cursor + 1 >= buffer.Length) { name = null; return false; }
                if (++hops > 32) { name = null; return false; }
                int pointer = ((len & 0x3F) << 8) | buffer[cursor + 1];
                if (!returnOffset.HasValue) returnOffset = cursor + 2;
                cursor = pointer;
                continue;
            }
            if (cursor + 1 + len > buffer.Length) { name = null; return false; }
            if (sb.Length > 0) sb.Append('.');
            sb.Append(Encoding.UTF8.GetString(buffer, cursor + 1, len));
            cursor += 1 + len;
        }
    }

    // ---- Name helpers -----------------------------------------------------------------

    private static string SanitizeInstanceName(string s) {
        if (string.IsNullOrWhiteSpace(s)) return "SecureArchive";
        return s.Replace('.', '_').Trim();
    }

    private static string SanitizeHostname(string s) {
        if (string.IsNullOrWhiteSpace(s)) return "securearchive";
        return s.Replace('.', '-').Replace(' ', '-').Trim();
    }

    private static List<IPAddress> GetLocalIPv4Addresses() {
        var list = new List<IPAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in nic.GetIPProperties().UnicastAddresses) {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;
                list.Add(ua.Address);
            }
        }
        return list.Distinct().ToList();
    }
}
