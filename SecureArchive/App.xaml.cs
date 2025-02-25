using io.github.toyota32k.toolkit.net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Utils;
using SecureArchive.Views;
using SecureArchive.Views.ViewModels;

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

        private class DebugTracer : ILogTracer {
            private ILogger logger;
            public DebugTracer(ILogger logger) {
                this.logger = logger;
            }
            public void trace(io.github.toyota32k.toolkit.net.LogLevel level, string message) {
                switch (level) {
                    case io.github.toyota32k.toolkit.net.LogLevel.INFO:
                        logger.LogInformation(message);
                        break;
                    case io.github.toyota32k.toolkit.net.LogLevel.WARN:
                        logger.LogWarning(message);
                        break;
                    case io.github.toyota32k.toolkit.net.LogLevel.ERROR:
                        logger.LogError(message);
                        break;
                    case io.github.toyota32k.toolkit.net.LogLevel.DEBUG:
                        logger.LogDebug(message);
                        break;
                }
            }
        }



        public static MainWindow MainWindow { get; } = new MainWindow();
        public static Frame RootFrame => MainWindow.RootFrame;

        public static UIElement? AppTitleBar => MainWindow.AppTitleBar;
        public ILogger Logger { get; }

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
                    .AddHttpClient()
                    .AddSingleton<IAppConfigService, AppConfigService>()
                    .AddSingleton<ILocalSettingsService, LocalSettingsService>()
                    .AddSingleton<IUserSettingsService, UserSettingsService>()
                    .AddSingleton<IPageService, PageService>()
                    .AddSingleton<IDatabaseService, DatabaseService>()
                    .AddSingleton<ICryptographyService, CryptographyService>()
                    .AddSingleton<IPasswordService, PasswordService>()
                    .AddSingleton<IFileStoreService, FileStoreService>()
                    .AddSingleton<ISecureStorageService, SecureStorageService>()
                    .AddSingleton<IHttpServreService,  HttpServerService>()
                    .AddSingleton<IMainThreadService, MainThradService>()
                    .AddSingleton<IBackupService, BackupService>()
                    .AddSingleton<IDeviceMigrationService, DeviceMigrationService>()
                    .AddTransient<ITaskQueueService, TaskQueueService>()
                    .AddSingleton<ISyncArchiveService, SyncArchiveSevice>()
                    .AddTransient<IStatusNotificationService, StatusNotificationService>()
                    .AddLogging(builder => {
                        builder.AddFilter(level => true);
                        builder.AddConsole();
                    })
                    .AddSingleton<MainWindowViewModel>()
                    .AddTransient<MenuPageViewModel>()
                    .AddTransient<SettingsPageViewModel>()
                    .AddSingleton<ListPageViewModel>()
                    .AddTransient<BackupDialogViewModel>()
                    .AddTransient<DeleteBackupDialogViewModel>()
                    .AddTransient<UpdateBackupDialogViewModel>()
                    .AddTransient<RemotePasswordDialogViewModel>()
                    .AddTransient<RemotePasswordDialogPage>()
                    .AddTransient<SyncArchiveDialogViewModel>()
                    .AddTransient<SyncArchiveDialogPage>()
                    ;
                })
                .Build();

            Logger = GetService<ILoggerFactory>().CreateLogger("");
            UtLog.SetGlobalLogger(Logger);
            UnhandledException += OnUnhandledException;

            io.github.toyota32k.toolkit.net.Logger.Tracer = new DebugTracer(Logger);
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) {
            Logger.Error(e.Exception, "Unhandled Exception");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            GetService<IPageService>().Startup(RootFrame);
            MainWindow.Activate();
            TitleBarHelper.ApplySystemThemeToCaptionButtons();

            //var ds = GetService<IDatabaseService>();
            //ds.EditKVs((kvs) => {
            //    kvs.SetString("hoge", "fuga");
            //    return true;
            //});
            MainWindow.AppWindow.Closing += AppWindow_Closing;
        }

        /**
         * アプリ終了前に確認ダイアログを表示する
         */
        private bool closeConfirmed = false;
        private async void ConfirmClose() {
            var r = await MessageBoxBuilder.Create(MainWindow)
                .SetMessage("Are you sure you want to exit?")
                .AddButton("Yes", id: true)
                .AddButton("No", id: false)
                .ShowAsync() as bool?;
            if(r==true) {
                closeConfirmed = true;
                MainWindow.Close();
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            if (!closeConfirmed) {
                args.Cancel = true;
                ConfirmClose();
            }
            GetService<IHttpServreService>().Stop();
        }
    }
}
