using SecureArchive.Utils;

namespace SecureArchive.DI.Impl.settings {
    internal class JSONSettngStore : ISettingsStore {
        private bool _isInitialized = false;
        private IDictionary<string, object> _settings = null!;
        private string _userSettingsFile;

        public JSONSettngStore (string userSettingFile) {
            _userSettingsFile = userSettingFile;
        }

        private async Task InitializeAsync() {
            if (!_isInitialized) {
                _isInitialized = true;
                _settings = await Task.Run(() => JsonFileHelper.Read<IDictionary<string, object>>(_userSettingsFile) ?? new Dictionary<string, object>());
            }
        }

        public async Task<T?> GetAsync<T>(string key) {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj)) {
                return await Json.ToObjectAsync<T>((string)obj);
            } else {
                return default;
            }
        }

        public async Task PutAsync<T>(string key, T value) {
            await InitializeAsync();
            _settings[key] = await Json.StringifyAsync(value);
            await Task.Run(() => JsonFileHelper.Save(_userSettingsFile, _settings));
        }
        public async Task DeleteAsync<T>(string key) {
            await InitializeAsync();
            if(_settings.ContainsKey(key)) {
                _settings.Remove(key);
                if(_settings.Count==0) {
                    JsonFileHelper.Delete(_userSettingsFile);
                } else {
                    JsonFileHelper.Save(_userSettingsFile,_settings);
                }
            }
        }
    }
}
