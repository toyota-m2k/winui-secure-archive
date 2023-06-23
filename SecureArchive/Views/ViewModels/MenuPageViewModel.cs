using Reactive.Bindings;
using SecureArchive.DI;

namespace SecureArchive.Views.ViewModels {
    internal class MenuPageViewModel {
        IPageService _pageService;
        IHttpServreService _httpServreService;
        IUserSettingsService _userSettingsService;

        public ReactiveCommandSlim ListCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim SettingsCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim ClearAllCommand { get; } = new ReactiveCommandSlim();
        public ReactivePropertySlim<bool> IsServerRunning { get; } = new ReactivePropertySlim<bool>(false);

        public MenuPageViewModel(
            IPageService pageSercice, 
            IHttpServreService httpServreService, 
            IUserSettingsService userSettingsService) {
            _pageService = pageSercice;
            _httpServreService = httpServreService;
            _userSettingsService = userSettingsService;

            SettingsCommand.Subscribe(pageSercice.ShowSettingsPage);
            ListCommand.Subscribe(pageSercice.ShowListPage);
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
            InitServer();
        }

        private async void InitServer() {
            if((await _userSettingsService.GetAsync()).ServerAutoStart) {
                StartServer();
            }
        }

        private async void StartServer() {
            _httpServreService.Start((await _userSettingsService.GetAsync()).PortNo);
        }
        private void StopServer() {
            _httpServreService.Stop();
        }
    }
}
