using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecureArchive.Utils;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SecureArchive.Views;

/// <summary>
/// 自己署名サーバー証明書を生成するための ContentDialog。
/// PrimaryButton = Generate / CloseButton = Cancel。Generate 押下で <see cref="Result"/> に書き出す。
/// </summary>
public sealed partial class GenerateCertDialog : ContentDialog {
    /// <summary>呼び出し側 (ViewModel) が初期値として渡す。</summary>
    public string InitialPfxPath { get; set; } = "";
    public string InitialPassword { get; set; } = "";
    public string InitialSubject { get; set; } = "";

    public class GenerateResult {
        public string PfxPath { get; }
        public string Password { get; }
        public GenerateResult(string pfxPath, string password) {
            PfxPath = pfxPath;
            Password = password;
        }
    }

    /// <summary>Generate 成功後にセットされる。Cancel / 失敗時は null。</summary>
    public GenerateResult? Result { get; private set; }

    public GenerateCertDialog() {
        InitializeComponent();
        Loaded += OnLoaded;
        PrimaryButtonClick += OnPrimary;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        var hostName = Environment.MachineName;
        SubjectBox.Text = string.IsNullOrWhiteSpace(InitialSubject)
            ? $"SecureArchive-{hostName}"
            : InitialSubject;
        DnsBox.Text = string.Join(", ", new[] { hostName, "localhost" }.Distinct());
        try {
            var ips = CertificateGenerator.GetLocalIPv4Addresses();
            IpBox.Text = string.Join(", ", ips.Select(ip => ip.ToString()));
        }
        catch { IpBox.Text = ""; }

        if (!string.IsNullOrEmpty(InitialPfxPath)) {
            PfxPathBox.Text = InitialPfxPath;
        }
        else {
            // 既定の出力先: アプリ実行フォルダ + securearchive.pfx
            var defaultDir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? Environment.CurrentDirectory;
            PfxPathBox.Text = Path.Combine(defaultDir, "securearchive.pfx");
        }
        PasswordBox.Text = string.IsNullOrEmpty(InitialPassword) ? GenerateRandomPassword(16) : InitialPassword;
    }

    private async void OnBrowseClicked(object sender, RoutedEventArgs e) {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.FileTypeChoices.Add("PFX certificate", new System.Collections.Generic.List<string> { ".pfx" });
        picker.SuggestedFileName = "securearchive";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSaveFileAsync();
        if (file != null) {
            PfxPathBox.Text = file.Path;
        }
    }

    private void OnRandomPasswordClicked(object sender, RoutedEventArgs e) {
        PasswordBox.Text = GenerateRandomPassword(16);
    }

    private static string GenerateRandomPassword(int length) {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        using var rng = RandomNumberGenerator.Create();
        var buf = new byte[length];
        rng.GetBytes(buf);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++) sb.Append(chars[buf[i] % chars.Length]);
        return sb.ToString();
    }

    private void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
        ErrorText.Text = "";
        try {
            var subject = SubjectBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(subject)) { ErrorText.Text = "Subject CN is required."; args.Cancel = true; return; }
            var pfxPath = PfxPathBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(pfxPath)) { ErrorText.Text = "PFX path is required."; args.Cancel = true; return; }

            var dnsList = SplitList(DnsBox.Text);
            var ipList = new System.Collections.Generic.List<IPAddress>();
            foreach (var s in SplitList(IpBox.Text)) {
                if (!IPAddress.TryParse(s, out var ip)) { ErrorText.Text = $"Invalid IP: {s}"; args.Cancel = true; return; }
                ipList.Add(ip);
            }
            if (dnsList.Count == 0 && ipList.Count == 0) {
                ErrorText.Text = "At least one DNS name or IP address is required.";
                args.Cancel = true;
                return;
            }

            int validity = (int)ValidityBox.Value;
            if (validity < 1 || validity > 100) { ErrorText.Text = "Validity 1..100"; args.Cancel = true; return; }

            CertificateGenerator.GenerateAndExport(
                subject, dnsList, ipList, validity, pfxPath, PasswordBox.Text ?? "");

            Result = new GenerateResult(pfxPath, PasswordBox.Text ?? "");
        }
        catch (Exception ex) {
            ErrorText.Text = $"Failed: {ex.Message}";
            args.Cancel = true;
        }
    }

    private static System.Collections.Generic.List<string> SplitList(string? s) {
        if (string.IsNullOrWhiteSpace(s)) return new System.Collections.Generic.List<string>();
        return s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
    }
}
