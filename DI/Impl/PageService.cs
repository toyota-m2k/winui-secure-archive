using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.TinyLinq;
using SecureArchive.Views;

namespace SecureArchive.DI.Impl;

internal class PageService : IPageService {
    Frame _rootFrame = null!;
    IFileStoreService _fileStoreService;
    private ReactivePropertySlim<Type?> _currentPageType = new(null);

    public ReadOnlyReactivePropertySlim<bool> CanGoBack { get; }

    public bool CheckPasswordRequired { get; private set; } = true;

    public PageService(IFileStoreService fileStoreService) {
        _fileStoreService = fileStoreService;
        CanGoBack = _currentPageType.Select((it) => { return it != null && it != typeof(MenuPage); }).ToReadOnlyReactivePropertySlim<bool>();
    }

    public void Startup(Frame rootFrame) {
        _rootFrame = rootFrame;
        rootFrame.Navigated += OnNavigated;
        ShowSettingsPage();
    }

    private void OnNavigated(object sender, NavigationEventArgs e) {
        if(sender is Frame frame) {
            // BackStackは自力で管理する。
            frame.BackStack.Clear();
        }
    }

    public void GoBack() {
        if(CanGoBack.Value) { 
            ShowMenuPage();
        }
    }

    private void NavigateTo(Type pageType) {
        if (_currentPageType.Value != pageType) {
            _currentPageType.Value = pageType;
            _rootFrame.Navigate(pageType);
        }

    }

    public void ShowMenuPage() {
        CheckPasswordRequired = false;
        NavigateTo(typeof(MenuPage));
    }

    public void ShowListPage() {
        CheckPasswordRequired = false;
        NavigateTo(typeof(ListPage));
    }

    public void ShowSettingsPage() {
        NavigateTo(typeof(SettingsPage));
    }

    public void CheckPasswordPage() {
        CheckPasswordRequired = true;
        NavigateTo(typeof(SettingsPage));
    }
}
