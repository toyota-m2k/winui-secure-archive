using SecureArchive.DI.Impl.settings;

namespace SecureArchive.DI.Impl;
internal class LocalSettingsService : ILocalSettingsService {
    private ISettingsStore _userSettings;

    public LocalSettingsService(IAppConfigService appConfig) {
        //_appConfigService = appConfig;
        var appPath = appConfig.AppDataPath;
        if (!Directory.Exists(appPath)) {
            Directory.CreateDirectory(appPath);
        }
        _userSettings = new JSONSettngStore(appConfig.SettingsPath);

        //if (appConfig.IsMSIX) {
        //    _userSettings = new MSIXSettingsStore();
        //}
        //else {
        //    var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        //    var appPath = appConfig.AppDataPath;
        //    if (!Directory.Exists(appPath)) {
        //        Directory.CreateDirectory(appPath);
        //    }
        //    _userSettings = new JSONSettngStore(Path.Combine(appPath, USER_SETTINGS_FILE_NAME));
        //}
    }

    public async Task<T?> GetAsync<T>(string key) {
        return await _userSettings.GetAsync<T>(key);
    }

    public async Task PutAsync<T>(string key, T value) {
        await _userSettings.PutAsync(key, value);
    }
}
