using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
//using System.Windows.Controls;

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

            ViewModel.ExportCommand.Subscribe(() => {
                _ = ViewModel.ExportFiles(FileListGrid.SelectedItems.Cast<FileEntry>().ToList());
            });
            ViewModel.PatchCommand.Subscribe(() => {
                _ = ViewModel.ConvertFastStart(FileListGrid.SelectedItems.Cast<FileEntry>().ToList());
            });
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

        private void FileListGrid_Sorting(object sender, DataGridColumnEventArgs e) {
            var tag = e.Column.Tag as string;
            if (tag == null) return;

            e.Column.SortDirection = ViewModel.SortBy(tag) ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;
            foreach(var col in FileListGrid.Columns) {
                if(col != e.Column) {
                    col.SortDirection = null;
                }
            }
        }
    }
}
