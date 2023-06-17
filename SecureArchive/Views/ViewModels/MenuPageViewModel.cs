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

        public MenuPageViewModel(IPageService pageSercice, IHttpServreService httpServreService, IUserSettingsService userSettingsService) {
            _pageService = pageSercice;
            _httpServreService = httpServreService;
            _userSettingsService = userSettingsService;

            SettingsCommand.Subscribe(pageSercice.ShowSettingsPage);
            ListCommand.Subscribe(pageSercice.ShowListPage);
        }

        public async void StartServer() {
            _httpServreService.Start((await _userSettingsService.GetAsync()).PortNo);
        }
        public void StopServer() {
            _httpServreService.Stop();
        }
    }
}
