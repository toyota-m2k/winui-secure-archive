using Microsoft.UI.Xaml;
using SecureArchive.Utils;
using System.Diagnostics;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;

namespace SecureArchive.DI.Impl {
    internal class AppConfigService : IAppConfigService {
        public string AppTitle => "AppTitle/Text".GetLocalized();

        public string AppIconPath => Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico");

        public string AppName { get; }

        public Version AppVersion { get; }

        public bool IsMSIX { get; }

        private string? customAppDataPath = null;

        public string AppDataPath {
            get {
                if (!string.IsNullOrEmpty(customAppDataPath)) {
                    return customAppDataPath!;
                }
                else if (IsMSIX) {
                    return ApplicationData.Current.LocalFolder.Path;
                } else {
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
                    if (!Directory.Exists(path)) {
                        Directory.CreateDirectory(path);
                    }
                    return path;
                }
            }
        }
        public string DBName => $"{AppName}.db";
        public string DBPath => Path.Combine(AppDataPath, DBName);

        public string SettingsName => "UserSettings.json";
        public string SettingsPath => Path.Combine(AppDataPath, SettingsName);


        public AppConfigService(string? appDataPath) {
            customAppDataPath = appDataPath;
            IsMSIX = RuntimeHelper.IsMSIX;
            if (IsMSIX) {
                var package = Package.Current;
                AppName = package.DisplayName;
                var v = Package.Current.Id.Version;
                AppVersion = new(v.Major, v.Minor, v.Build, v.Revision);
            } else {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                AppName = assemblyName.Name ?? "who am i?";
                AppVersion = assemblyName.Version ?? new Version(0,0,0,0);
            }
            var appPath = AppDataPath;
            if (!Directory.Exists(appPath)) {
                Directory.CreateDirectory(appPath);
            }
            Debug.WriteLine(AppName + " " + AppVersion);
            var exePath = Process.GetCurrentProcess().MainModule!.FileName;
            Debug.WriteLine(exePath);
        }

        public bool NeedsConfirmOnExit { get; set; } = false;

        public void Restart() {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName;
            var proc = Process.Start(exePath);
            Application.Current.Exit();
        }
    }
}
