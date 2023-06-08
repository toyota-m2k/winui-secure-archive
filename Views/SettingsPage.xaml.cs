using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SecureArchive.Views.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive.Views;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsPage : Page {
    private SettingsPageViewModel ViewModel { get; }
    public SettingsPage() {
        ViewModel = App.GetService<SettingsPageViewModel>();
        this.InitializeComponent();
    }

    private void HandleEnterKey(object sender, KeyRoutedEventArgs e) {
        if(e.Key == Windows.System.VirtualKey.Enter) {
            if(ViewModel.PasswordReady.Value) {
                ViewModel.PasswordCommand.Execute();
            }
        }
    }
}
