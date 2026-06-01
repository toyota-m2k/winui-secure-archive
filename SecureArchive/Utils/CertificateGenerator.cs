using SecureArchive.Utils.Server.mdns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SecureArchive.Utils;

public static class CertificateGenerator {

    /// <summary>
    /// 証明書の SHA-256 フィンガープリントを "AB:CD:..." 形式で返す。
    /// mDNS TXT レコードや UI 表示で使う。
    /// </summary>
    public static string ComputeSha256Fingerprint(X509Certificate2 cert) {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(cert.RawData);
        return BitConverter.ToString(hash).Replace("-", ":");
    }

    /// <summary>
    /// アクティブなネットワークインタフェース上の IPv4 アドレスを列挙する（ループバック除く）。
    /// </summary>
    public static IEnumerable<IPAddress> GetLocalIPv4Addresses() {
        return MdnsCommon.MyAddresses;

        //return NetworkInterface.GetAllNetworkInterfaces()
        //    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
        //              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        //    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
        //    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork
        //                && !IPAddress.IsLoopback(addr.Address))
        //    .Select(addr => addr.Address)
        //    .Distinct()
        //    .ToList();
    }

    /// <summary>
    /// 自己署名サーバー証明書を生成し、PFX (.pfx) と公開証明書 (.cer) をディスクに書き出す。
    /// .cer はクライアント配布用（PFX と同じディレクトリ・同じベース名で出力）。
    /// </summary>
    public static void GenerateAndExport(
            string subjectCommonName,
            IEnumerable<string> dnsNames,
            IEnumerable<IPAddress> ipAddresses,
            int validityYears,
            string pfxPath,
            string password) {
        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName($"CN={subjectCommonName}");
        var req = new CertificateRequest(
            subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // CA ではない（end-entity 証明書）
        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        req.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));

        // EKU: serverAuth (1.3.6.1.5.5.7.3.1)
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                true));

        // Subject Alternative Name (DNS / IP)
        var sanBuilder = new SubjectAlternativeNameBuilder();
        foreach (var dns in dnsNames.Where(s => !string.IsNullOrWhiteSpace(s))) {
            sanBuilder.AddDnsName(dns.Trim());
        }
        foreach (var ip in ipAddresses) {
            sanBuilder.AddIpAddress(ip);
        }
        req.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.Now.AddDays(-1);
        var notAfter = DateTimeOffset.Now.AddYears(Math.Max(1, validityYears));

        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        var dir = Path.GetDirectoryName(pfxPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }

        // PFX (秘密鍵を含む)
        var pfxBytes = cert.Export(X509ContentType.Pfx, password ?? "");
        File.WriteAllBytes(pfxPath, pfxBytes);

        // 公開鍵 (.cer) — クライアント側で trusted root に入れるための配布用
        var cerPath = Path.ChangeExtension(pfxPath, ".cer");
        var cerBytes = cert.Export(X509ContentType.Cert);
        File.WriteAllBytes(cerPath, cerBytes);
    }
}
