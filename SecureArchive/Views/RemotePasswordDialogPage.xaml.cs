using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive.Views; 
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class RemotePasswordDialogPage : Page, IStandardDialogPage<string?>, ICustomDialogPage<string?> {
    public RemotePasswordDialogViewModel ViewModel { get; }
    public ContentDialog Dialog { get; set; } = null!;
    public event Action<string?>? Complete;

    public RemotePasswordDialogPage() {
        ViewModel = App.GetService<RemotePasswordDialogViewModel>();
        this.InitializeComponent();
    }

    public string? GetResult(ContentDialogResult contentDialogResult) {
        if(contentDialogResult==ContentDialogResult.Primary) {
            return ViewModel.Password.Value;
        } else {
            return null;
        }
    }

    private void HandleEnterKey(object sender, KeyRoutedEventArgs e) {
        if (e.Key == Windows.System.VirtualKey.Enter && ViewModel.IsReady.Value) {
            Complete?.Invoke(ViewModel.Password.Value);
        }
    }

    public async Task<string?> GetPassword(XamlRoot parent) {
        return await CustomDialogBuilder<RemotePasswordDialogPage,string?>.Create(parent, this)
            .SetPrimaryButton("OK")
            .SetSecondaryButton("Cancel")
            .ShowAsync();
    }
}
