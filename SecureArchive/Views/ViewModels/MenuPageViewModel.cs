using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Security.Cryptography.X509Certificates;

namespace SecureArchive.Views.ViewModels {
    internal class MenuPageViewModel {
        IPageService _pageService;
        IHttpServreService _httpServreService;
        IUserSettingsService _userSettingsService;
        ISecureStorageService _secureStorageService;

        public ReactiveCommandSlim ListCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim SettingsCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim MirrorCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim QRCodeCommand { get; } = new ReactiveCommandSlim();
        //public ReactiveCommandSlim RepairCommand { get; } = new ReactiveCommandSlim();
        public ReactivePropertySlim<bool> IsServerRunning { get; } = new ReactivePropertySlim<bool>(false, ReactivePropertyMode.DistinctUntilChanged);
        public ReactivePropertySlim<bool> ShowLog { get; } = new ReactivePropertySlim<bool>(false, ReactivePropertyMode.DistinctUntilChanged);
        public ReadOnlyReactivePropertySlim<VerticalAlignment> PanelVerticalAlignment { get; }
        public ReactivePropertySlim<string> ServerParams { get; } = new ReactivePropertySlim<string>("");

        public MenuPageViewModel(
            //ILoggerFactory loggerFactory,
            IPageService pageSercice, 
            IHttpServreService httpServreService, 
            ISecureStorageService secureStorageService,
            //ISyncArchiveService syncArchiveService,
            IUserSettingsService userSettingsService) {
            //_logger = loggerFactory.CreateLogger("MenuPage");
            _pageService = pageSercice;
            _httpServreService = httpServreService;
            _secureStorageService = secureStorageService;
            _userSettingsService = userSettingsService;

            SettingsCommand.Subscribe(pageSercice.ShowSettingsPage);
            ListCommand.Subscribe(pageSercice.ShowListPage);
            MirrorCommand.Subscribe(() => {
                App.GetService<SyncArchiveDialogPage>().ShowDialog(_pageService.CurrentPage!.XamlRoot);
            });
            QRCodeCommand.Subscribe(ShowPairingQr);
            //RepairCommand.Subscribe(() => {
            //    Repair();
            //});
            IsServerRunning.Subscribe((it) => {
                if (it) {
                    StartServer();
                }
                else {
                    StopServer();
                }
            });
            _httpServreService.Running.Subscribe((it) => {
                IsServerRunning.Value = it;
            });
            ShowLog.Subscribe((it) => {
                _userSettingsService.EditAsync(us => {
                    us.ShowLog = it;
                    return true;
                });
            });
            PanelVerticalAlignment = ShowLog.Select(it => it ? VerticalAlignment.Stretch : VerticalAlignment.Center).ToReadOnlyReactivePropertySlim(VerticalAlignment.Center);
            InitServer();
        }

        private async void InitServer() {
            var us = await _userSettingsService.GetAsync();
            ShowLog.Value = us.ShowLog;
            if (us.EnableHttp && us.EnableHttps) {
                ServerParams.Value = $"HTTP: {us.PortHttp} / HTTPS: {us.PortHttps}";
            }
            else if (us.EnableHttps) {
                ServerParams.Value = $"HTTPS: {us.PortHttps}";
            }
            else if (us.EnableHttp) {
                ServerParams.Value = $"HTTP: {us.PortHttp}";
            }
            else {
                ServerParams.Value = "Server is disabled";
            }
            if (us.ServerAutoStart) {
                StartServer();
            }
        }

        private async void StartServer() {
            _httpServreService.Start();
        }
        private void StopServer() {
            _httpServreService.Stop();
        }

        //private async void Repair() {
        //    await Task.Run(_secureStorageService.ConvertFastStart);
        //    //using (var inStream = File.OpenRead(@"c:\temp\xxxxx\mov-2023.04.16-00：12：26.mp4")) {
        //    //    bool needfs = await MovieFastStart.Check(inStream);
        //    //    //_logger.Info($"need fast start = {needfs}");
        //    //    if (needfs) {
        //    //        inStream.Seek(0, SeekOrigin.Begin);
        //    //        bool result = await MovieFastStart.Process(inStream, () => new FileStream(@"c:\temp\xxxxx\a.mp4", FileMode.Create, FileAccess.Write, FileShare.None), null);
        //    //        //_logger.Info($"FastStart = {result}");
        //    //    }
        //    //}

        //}

        private async void ShowPairingQr() {
            // Persist current edits so the QR reflects the latest settings
            var settings = await _userSettingsService.GetAsync();
            var dlg = new Views.PairingQrDialog {
                XamlRoot = App.MainWindow.Content.XamlRoot,
                ServerName = settings.EnsureServerName,
                Port = settings.EnableHttps ? settings.PortHttps : settings.PortHttp,
                IsHttps = settings.EnableHttps,
                Fingerprint = ComputeFingerprintIfPossible(settings),
            };
            await dlg.ShowAsync();
        }

        private static string ComputeFingerprintIfPossible(IReadonlyUserSettingsAccessor settings) {
            if (!settings.EnableHttps) return "";
            if (string.IsNullOrEmpty(settings.PfxPath) || !File.Exists(settings.PfxPath)) return "";
            try {
                // EphemeralKeySet: MSIX サンドボックス下でも OS ストアアクセスを発生させない
                using var cert = X509CertificateLoader.LoadPkcs12FromFile(
                    settings.PfxPath!, settings.PfxPassword, 
                    X509KeyStorageFlags.EphemeralKeySet);
                //using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                //    settings.PfxPath!, settings.PfxPassword,
                //    System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);
                return Utils.CertificateGenerator.ComputeSha256Fingerprint(cert);
            }
            catch (Exception e) {
                Debug.WriteLine(e);
                return "";
            }
        }

    }
}
