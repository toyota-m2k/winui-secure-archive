using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings;

namespace SecureArchive.DI;

interface INavigationAware {
    void OnPageActivated();
    void OnPageLeaving();
}

/**
 * ページのリストを管理して、その切り替えをサポートするサービス
 */
internal interface IPageService {
    bool CheckPasswordRequired { get; }
    ReadOnlyReactivePropertySlim<bool> CanGoBack { get; }
    void Startup(Frame rootFrame);
    void ShowMenuPage();
    void ShowSettingsPage();
    void ShowListPage();
    void GoBack();
    Page? CurrentPage { get; }
}
