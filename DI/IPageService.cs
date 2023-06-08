using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI {
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
    }
}
