using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SecureArchive.Utils.Server {
    /// <summary>
    /// MdnsAdvertiser / MdnsBrowser 共通の定数と DNS パケット読み書きプリミティブ。
    /// </summary>
    internal static class MdnsCommon {
        // 全 BooTube プロトコル互換アプリで共用するサービスタイプ。
        public const string ServiceType = "_booapi._tcp";
        // このアプリ識別子 (TXT app=...)。
        public const string AppId = "SA";
        public const int MdnsPort = 5353;
        public static readonly IPAddress MulticastV4 = IPAddress.Parse("224.0.0.251");

        // ---- Writing ----------------------------------------------------------------------

        public static void WriteName(MemoryStream ms, string fqdn) {
            // Name compression は省略 (パケットサイズは増えるが実装が単純で済む)
            foreach (var label in fqdn.Split('.')) {
                if (string.IsNullOrEmpty(label)) continue;
                var bytes = Encoding.UTF8.GetBytes(label);
                if (bytes.Length > 63) {
                    // 63 バイトを超えるラベルは仕様外なので切り詰め
                    Array.Resize(ref bytes, 63);
                }
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
        }

        public static void WriteUInt16(MemoryStream ms, ushort v) {
            ms.WriteByte((byte)(v >> 8));
            ms.WriteByte((byte)(v & 0xFF));
        }

        public static void WriteUInt32(MemoryStream ms, uint v) {
            ms.WriteByte((byte)((v >> 24) & 0xFF));
            ms.WriteByte((byte)((v >> 16) & 0xFF));
            ms.WriteByte((byte)((v >> 8) & 0xFF));
            ms.WriteByte((byte)(v & 0xFF));
        }

        // ---- Reading ----------------------------------------------------------------------

        public static ushort ReadUInt16(byte[] buf, ref int off) {
            ushort v = (ushort)((buf[off] << 8) | buf[off + 1]);
            off += 2;
            return v;
        }

        public static uint ReadUInt32(byte[] buf, ref int off) {
            uint v = (uint)((buf[off] << 24) | (buf[off + 1] << 16) | (buf[off + 2] << 8) | buf[off + 3]);
            off += 4;
            return v;
        }

        /// <summary>
        /// バッファ中の DNS 形式の名前を読み取る。コンプレッションポインタ (0xC0xx) も追従する。
        /// 末尾に "." を付けた fqdn を返す。失敗時 false。
        /// </summary>
        public static bool TryReadName(byte[] buffer, ref int offset, out string name) {
            var sb = new StringBuilder();
            int cursor = offset;
            int? returnOffset = null; // 圧縮ポインタを辿った場合の元の位置
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
                    if (++hops > 32) { name = null; return false; } // ループ防止
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

        // ---- NIC enumeration --------------------------------------------------------------

        /// <summary>
        /// マルチキャスト対応の UP な NIC のユニキャスト IPv4 アドレスを列挙する。
        /// mDNS サブシステムでの socket bind 用。Advertizer / Browser 共通。
        /// </summary>
        public static IEnumerable<IPAddress> EnumerateMulticastV4Addresses() {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (!nic.SupportsMulticast) continue;
                var ipProps = nic.GetIPProperties();
                foreach (var ua in ipProps.UnicastAddresses) {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    yield return ua.Address;
                }
            }
        }
    }
}
