using SecureArchive.Utils;
using System.Diagnostics;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.Storage;

namespace SecureArchive.DI.Impl {
    internal class AppConfigService : IAppConfigService {
        public string AppTitle => "AppTitle/Text".GetLocalized();

        public string AppIconPath => Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico");

        public string AppName { get; }

        public Version AppVersion { get; }

        public bool IsMSIX { get; }

        public string AppDataPath {
            get {
                if (IsMSIX) {
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

        public string DBPath => Path.Combine(AppDataPath, $"{AppName}.db");

        public AppConfigService() {
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
            Debug.WriteLine(AppName + " " + AppVersion);
        }
    }
}
