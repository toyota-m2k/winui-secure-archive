using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecureArchive.DI.Impl;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive.Views; 
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DeleteBackupDialogPage : Page, ICustomDialogPage<bool> {
    private DeleteBackupDialogViewModel ViewModel { get; }
    public ContentDialog Dialog { get; set; } = null!;
    public event Action<bool>? Complete = null;

    public DeleteBackupDialogPage() {
        ViewModel = App.GetService<DeleteBackupDialogViewModel>();
        this.InitializeComponent();
        TargetListView.SelectionChanged += (s, e) => {
            ViewModel.Selected.Value = (TargetListView.SelectedItems.Count > 0);
        };
        ViewModel.DeleteCommand.Subscribe(() => {
            ViewModel.Delete(TargetListView.SelectedItems.Select((it) => (FileEntry)it).ToList());
        });
        ViewModel.CloseCommand.Subscribe(() => {
            Complete?.Invoke(true);
        });
        ViewModel.SelectAllCommand.Subscribe(TargetListView.SelectAll);
    }
}
