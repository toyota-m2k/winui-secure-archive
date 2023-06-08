using Microsoft.UI.Xaml;
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
        ViewModel.PanelStatus.Subscribe(async (status) => {
            UIElement? control = null;
            switch (status) {
                case SettingsPageViewModel.Status.Initializing:
                    return;
                case SettingsPageViewModel.Status.NeedToSetPassword:
                    control = EditPasswordSet;
                    break;
                case SettingsPageViewModel.Status.NeedToCheckPassword:
                    control = EditPasswordCheck;
                    break;
                case SettingsPageViewModel.Status.NeedToSetDataFolder:
                    control = ButtonSelectFolder;
                    break;
                case SettingsPageViewModel.Status.Ready:
                    control = ButtonDone;
                    break;
            }
            await Task.Delay(1000);
            control?.Focus(FocusState.Programmatic);
        });
    }

    private void HandleEnterKey(object sender, KeyRoutedEventArgs e) {
        if(e.Key == Windows.System.VirtualKey.Enter) {
            if(ViewModel.PasswordReady.Value) {
                ViewModel.PasswordCommand.Execute();
            }
        }
    }
}
