using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecureArchive.Utils;
using SecureArchive.Views.ViewModels;
using Windows.UI.ViewManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive; 
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, IMainFrameContract {
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
        // OS が描画する標準のタイトルバーを消し、ウィンドウの内容をタイトルバーの領域 (ウィンドウの最上部) まで広げます。
        // 最小化・最大化・閉じるのボタン (キャプションボタン) は残り、右上に重ねて表示されます。
        ExtendsContentIntoTitleBar = true;
        // 指定した要素をタイトルバーとして扱う領域にします。
        // この領域では、ドラッグでウィンドウを移動したり、ダブルクリックで最大化/元に戻したり、右クリックでシステムメニューを開いたりできます。
        SetTitleBar(_appTitleBar);
        //this.Content = RootFrame; <-- これはエラー。どうしてもやるなら、一旦、Context=null にしてからやること。



        Settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event
    }


    private void Settings_ColorValuesChanged(UISettings sender, object args) {
        // OS のテーマ(ライト/ ダーク) が切り替わったときに、タイトルバーの色を合わせる。
        DispatcherQueue.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args) {
        // 標準のタイトルバーと同じ見た目にするため、
        // ウィンドウが非アクティブになったときにタイトル文字の色を薄くします。
        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";
        _appTitleBar.TitleText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        //TitleBarHelper.ApplySystemThemeToCaptionButtons();    ここでやると無限ループになってスタックオーバーフローする
    }
}
