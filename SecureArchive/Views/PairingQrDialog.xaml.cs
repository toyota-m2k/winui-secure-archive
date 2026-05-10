using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SecureArchive.Views;

/// <summary>
/// ペアリング情報 (host:port + fingerprint + name) を QR で表示する ContentDialog。
/// QR の中身は `bootube://&lt;host&gt;:&lt;port&gt;?fp=...&amp;name=...&amp;svc=...&amp;https=...&amp;app=archive` 形式。
/// 互換クライアント (BooDroid 等) は bootube:// スキームの intent-filter で受け取る。
/// </summary>
public sealed partial class PairingQrDialog : ContentDialog {
    public string ServerName { get; set; } = "SecureArchive";
    public int Port { get; set; } = 3801;
    public bool IsHttps { get; set; } = false;
    public string Fingerprint { get; set; } = "";

    private List<string> _hostCandidates = new();

    public PairingQrDialog() {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        _hostCandidates = BuildHostCandidates();
        HostCombo.ItemsSource = _hostCandidates;
        if (_hostCandidates.Count > 0) HostCombo.SelectedIndex = 0;

        if (IsHttps && string.IsNullOrEmpty(Fingerprint)) {
            HintText.Text = "WARNING: HTTPS is enabled but the certificate fingerprint could not be computed. Clients won't be able to verify the server.";
        }
        else if (!IsHttps) {
            HintText.Text = "Note: HTTPS is not enabled. The QR will indicate http and clients won't pin a certificate.";
        }
    }

    private static List<string> BuildHostCandidates() {
        var list = new List<string>();
        try {
            foreach (var ip in CertificateGenerator.GetLocalIPv4Addresses()) {
                list.Add(ip.ToString());
            }
        }
        catch { }
        try {
            var name = Environment.MachineName;
            if (!string.IsNullOrEmpty(name)) list.Add(name + ".local");
        }
        catch { }
        if (list.Count == 0) list.Add("localhost");
        return list;
    }

    private async void OnHostChanged(object sender, SelectionChangedEventArgs e) {
        await UpdateQrAsync();
    }

    private async System.Threading.Tasks.Task UpdateQrAsync() {
        var host = HostCombo.SelectedItem as string ?? _hostCandidates.FirstOrDefault();
        if (host == null) return;

        var sb = new StringBuilder();
        sb.Append("bootube://"); // BooTube プロトコル互換クライアント (BooDroid) が intent-filter で拾う
        sb.Append(host);
        sb.Append(":").Append(Port);
        sb.Append("?fp=").Append(Uri.EscapeDataString(Fingerprint ?? ""));
        sb.Append("&name=").Append(Uri.EscapeDataString(ServerName ?? "SecureArchive"));
        sb.Append("&svc=").Append(Uri.EscapeDataString(ServerName ?? "SecureArchive"));
        sb.Append("&https=").Append(IsHttps ? "1" : "0");
        sb.Append("&app=archive");
        var uri = sb.ToString();

        UriBox.Text = uri;
        QrImage.Source = await GenerateQrAsync(uri);
    }

    private static async System.Threading.Tasks.Task<BitmapImage> GenerateQrAsync(string content) {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        var pngBytes = pngQr.GetGraphic(20);

        // PNG bytes → InMemoryRandomAccessStream → BitmapImage
        var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using (var writer = new Windows.Storage.Streams.DataWriter(ras)) {
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        ras.Seek(0);

        var bmp = new BitmapImage();
        await bmp.SetSourceAsync(ras);
        return bmp;
    }
}
