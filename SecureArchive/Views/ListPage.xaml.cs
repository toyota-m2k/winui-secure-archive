using CommunityToolkit.WinUI.UI.Controls.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
//using System.Windows.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ListPage : Page {
        private ListPageViewModel ViewModel { get; }
        public ListPage() {
            ViewModel = App.GetService<ListPageViewModel>();
            this.InitializeComponent();

            //ViewModel.ExportCommand.Subscribe(() => {
            //    _ = ViewModel.ExportFiles(FileListView.SelectedItems.Select((it)=>(FileEntry)it).ToList());
            //});
        }

        private void OnColumnHeaderClicked(object sender, RoutedEventArgs e) {

            var header = sender as DataGridColumnHeader;
            if (header == null) return;
            var tag = header.Tag as string ?? header.Content.ToString();
            if (tag == null) return;
            ViewModel._logger.Debug($"Sort by {tag}");
            //var sort = ViewModel.Sort;
            //if (sort == null) return;
            //if (sort.Key == tag) {
            //    ViewModel.Sort = (tag, !sort.Ascending);
            //}
            //else {
            //    ViewModel.Sort = (tag, true);
            //}
        }
    }
}
