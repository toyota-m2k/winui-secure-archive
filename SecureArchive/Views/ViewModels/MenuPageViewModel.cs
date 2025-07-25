﻿using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils;
using System.Reactive.Linq;

namespace SecureArchive.Views.ViewModels {
    internal class MenuPageViewModel {
        IPageService _pageService;
        IHttpServreService _httpServreService;
        IUserSettingsService _userSettingsService;
        ISecureStorageService _secureStorageService;

        public ReactiveCommandSlim ListCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim SettingsCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim MirrorCommand { get; } = new ReactiveCommandSlim();
        //public ReactiveCommandSlim RepairCommand { get; } = new ReactiveCommandSlim();
        public ReactivePropertySlim<bool> IsServerRunning { get; } = new ReactivePropertySlim<bool>(false, ReactivePropertyMode.DistinctUntilChanged);
        public ReactivePropertySlim<bool> ShowLog { get; } = new ReactivePropertySlim<bool>(false, ReactivePropertyMode.DistinctUntilChanged);
        public ReadOnlyReactivePropertySlim<VerticalAlignment> PanelVerticalAlignment { get; }
        public ReactivePropertySlim<int> PortNo { get; } = new ReactivePropertySlim<int>(0);

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
            PortNo.Value = us.PortNo;
            if (us.ServerAutoStart) {
                StartServer();
            }
        }

        private async void StartServer() {
            PortNo.Value = (await _userSettingsService.GetAsync()).PortNo;
            _httpServreService.Start(PortNo.Value);
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
    }
}
