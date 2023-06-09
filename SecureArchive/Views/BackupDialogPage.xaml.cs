using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecureArchive.DI.Impl;
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
public sealed partial class BackupDialogPage : Page, ICustomDialogPage<bool> {
    private BackupDialogViewModel ViewModel { get; }
    public ContentDialog Dialog { get; set; } = null!;
    public event Action<bool>? Complete = null;

    public BackupDialogPage() {
        ViewModel = App.GetService<BackupDialogViewModel>();
        this.InitializeComponent();
        TargetListView.SelectionChanged += (s, e) => {
            ViewModel.Selected.Value = (TargetListView.SelectedItems.Count > 0);
        };
        ViewModel.StartCommand.Subscribe(() => {
            ViewModel.Download(TargetListView.SelectedItems.Select((it) => (RemoteItem)it).ToList());
        });
        ViewModel.CloseCommand.Subscribe(() => {
            Complete?.Invoke(true);
        });
        ViewModel.SelectAllCommand.Subscribe(() => {
            TargetListView.SelectAll();
        });
    }
}
