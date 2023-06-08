using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
using System.Windows.Interop;
using Windows.UI.ViewManagement;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive; 
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : WindowEx, IMainFrameContract {
    public Frame RootFrame => _rootFrame;
    public UIElement AppTitleBar => _appTitleBar;

    private UISettings Settings = new ();

    private MainWindowViewModel ViewModel { get; }

    public MainWindow() {
        ViewModel = App.GetService<MainWindowViewModel>();
        this.InitializeComponent();
        

        // Alt+Tab でタスク一覧を表示したときのアイコン。。。
        // csproj <ApplicationIcon> と、AppWindow.SetIcon() の両方で指定しないと正しく表示されない。
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));

        this.Title = "AppTitle/Text".GetLocalized();

        // Custom Title Bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_appTitleBar);
        //this.Content = RootFrame; <-- これはエラー。どうしてもやるなら、一旦、Context=null にしてからやること。


        Settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event
    }

    private void Settings_ColorValuesChanged(UISettings sender, object args) {
        DispatcherQueue.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args) {
        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";
        _appTitleBar.TitleText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        //TitleBarHelper.ApplySystemThemeToCaptionButtons();    ここでやると無限ループになってスタックオーバーフローする
    }
}
