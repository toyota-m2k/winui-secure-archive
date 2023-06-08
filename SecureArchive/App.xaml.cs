using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Utils;
using SecureArchive.Views;
using SecureArchive.Views.ViewModels;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecureArchive {
    public interface IMainFrameContract {
        Frame RootFrame { get; }
        UIElement? AppTitleBar { get; }
    }

    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        public IHost Host {
            get;
        }

        public static T GetService<T>()
            where T : class {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service) {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        public static MainWindow MainWindow { get; } = new MainWindow();
        public static Frame RootFrame => MainWindow.RootFrame;

        public static UIElement? AppTitleBar => MainWindow.AppTitleBar;
        public ILogger logger { get; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            this.InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((context, service) => {
                    service
                    .AddSingleton<IAppConfigService, AppConfigService>()
                    .AddSingleton<ILocalSettingsService, LocalSettingsService>()
                    .AddSingleton<IUserSettingsService, UserSettingsService>()
                    .AddSingleton<IPageService,  PageService>()
                    .AddSingleton<IDataService, DataService>()
                    .AddSingleton<ICryptographyService, CryptographyService>()
                    .AddSingleton<IPasswordService, PasswordService>()
                    .AddSingleton<IFileStoreService, FileStoreService>()
                    .AddLogging(builder => {
                        builder.AddFilter(level => true);
                        builder.AddConsole();
                    })
                    .AddSingleton<MainWindowViewModel>()
                    .AddTransient<MenuPageViewModel>()
                    .AddTransient<SettingsPageViewModel>()
                    ;
                })
                .Build();

            logger = GetService<ILoggerFactory>().CreateLogger("App");
            UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) {
            logger.LogError(e.Exception, "Unhandled Exception");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            GetService<IPageService>().Startup(RootFrame);
            MainWindow.Activate();
            TitleBarHelper.ApplySystemThemeToCaptionButtons();
        }
    }
}
