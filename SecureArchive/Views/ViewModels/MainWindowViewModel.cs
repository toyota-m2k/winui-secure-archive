using Microsoft.UI.Xaml;
using Reactive.Bindings;
using SecureArchive.DI;
using System.Reactive.Linq;

namespace SecureArchive.Views.ViewModels {
    internal class MainWindowViewModel {
        private IAppConfigService _appConfigService;
        private IPageService _pageService;
        private string AppTitle => _appConfigService.AppTitle;
        public ReactivePropertySlim<string> Title { get; } = new("");
        public ReadOnlyReactivePropertySlim<bool> CanGoBack { get; }
        public ReadOnlyReactivePropertySlim<Visibility> GoBackVisibility { get; }
        public ReadOnlyReactivePropertySlim<string> WindowTitle { get; }
        public ReactiveCommandSlim GoBackCommand { get; } = new();

        public MainWindowViewModel(IAppConfigService appConfigService, IPageService pageService) {
            _appConfigService = appConfigService;
            _pageService = pageService;
            CanGoBack = pageService.CanGoBack;
            GoBackVisibility = CanGoBack.Select((it) => it ? Visibility.Visible : Visibility.Collapsed).ToReadOnlyReactivePropertySlim();
            WindowTitle =  Title.Select((it) => { return string.IsNullOrEmpty(it) ? AppTitle : $"{AppTitle}: {it}"; }).ToReadOnlyReactivePropertySlim<string>();
            GoBackCommand.Subscribe(_pageService.GoBack);
        }
    }
}
