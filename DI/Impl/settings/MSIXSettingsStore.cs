using SecureArchive.Utils;
using Windows.Storage;

namespace SecureArchive.DI.Impl.settings {
    internal class MSIXSettingsStore : ISettingsStore {
        public Task InitializeAsync() {
            return Task.CompletedTask;
        }

        public async Task<T?> GetAsync<T>(string key) {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj)) {
                return await Json.ToObjectAsync<T>((string)obj);
            }
            return default;
        }

        public async Task PutAsync<T>(string key, T value) {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value);
        }
        public Task DeleteAsync<T>(string key) {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
            return Task.CompletedTask;
        }

    }
}
