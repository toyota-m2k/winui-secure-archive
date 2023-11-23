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
public sealed partial class UpdateBackupDialogPage : Page, ICustomDialogPage<bool> {
    private UpdateBackupDialogViewModel ViewModel { get; }
    public ContentDialog Dialog { get; set; } = null!;
    public event Action<bool>? Complete = null;

    public UpdateBackupDialogPage() {
        ViewModel = App.GetService<UpdateBackupDialogViewModel>();
        this.InitializeComponent();
        ViewModel.CompleteCommand.Subscribe((result) => {
            Complete?.Invoke(result);
        });
        ViewModel.ExecuteUpdate();
    }

}
