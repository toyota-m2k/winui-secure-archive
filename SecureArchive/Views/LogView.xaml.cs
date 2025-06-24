using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecureArchive.Views.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive.Views;

public sealed partial class LogView : UserControl {
    private LogViewModel ViewModel { get; }
    public LogView() {
        ViewModel = App.GetService<LogViewModel>();
        this.InitializeComponent();

    }

    private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (ViewModel.StopScroll.Value) return;
        DispatcherQueue.TryEnqueue(() => {
            if (scrollViewer.ScrollableHeight > 0) {
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);
            }
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        (ViewModel.LogMessages as INotifyCollectionChanged).CollectionChanged += OnLogMessagesChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        (ViewModel.LogMessages as INotifyCollectionChanged).CollectionChanged -= OnLogMessagesChanged;
    }
}
