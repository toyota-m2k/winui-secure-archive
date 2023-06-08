using System.Runtime.CompilerServices;

namespace SecureArchive.DI.Impl;

internal class UserSettingsService : IUserSettingsService {
    private class SettingsEditor : IUserSettingsAccessor {
        private UserSettingsService _userSettings;
        public SettingsEditor(UserSettingsService parent) {
            _userSettings = parent;
        }
        private string callerName([CallerMemberName] string memberName = "") {
            return memberName;
        }


        public string? DataFolder {
            get => _userSettings.Get<string>(callerName());
            set => _userSettings.Put(callerName(), value);
        }
        public int PortNo { 
            get => _userSettings.Get<int>(callerName(), 6000);
            set => _userSettings.Put(callerName(), value);
        }
    }


    const string KEY_SETTINGS = "UserSettings";
    ILocalSettingsService _localSettingsService;
    Dictionary<string, object> _cache = null!;
    bool _dirty = false;

    public UserSettingsService(ILocalSettingsService localSettingsService) {
        _localSettingsService = localSettingsService;
    }

    private async Task InitializeAsync() {
        if (_cache == null) {
            _cache = await _localSettingsService.GetAsync<Dictionary<string, object>>(KEY_SETTINGS) ?? new Dictionary<string, object>();
        }
    }

    private async Task CommitAsync() {
        if (_dirty) {
            await _localSettingsService.PutAsync(KEY_SETTINGS, _cache);
            _dirty = false;
        }
    }

    //private T? Get<T>(string key) {
    //    if (_cache == null) {
    //        throw new InvalidOperationException("call InitializeAsync() in prior.");
    //    }
    //    if (_cache.TryGetValue(key, out var value)) {
    //        return (T)value;
    //    }
    //    else {
    //       return default(T?);
    //    }
    //}
    private T? Get<T>(string key, T? defalutValue = default) {
        if (_cache == null) {
            throw new InvalidOperationException("call InitializeAsync() in prior.");
        }
        if (_cache.TryGetValue(key, out var value)) {
            return (T)value;
        }
        else {
            return defalutValue;
        }
    }

    private T? Get<T>(SettingsKey key) {
        return Get<T>(key.ToString());
    }

    private void Put<T>(string key, T? value) {
        if (_cache == null) {
            throw new InvalidOperationException("call InitializeAsync() in prior.");
        }
        if (value == null) {
            if (_cache.ContainsKey(key)) {
                _cache.Remove(key);
                _dirty = true;
            }
        }
        else if (!value.Equals(Get<T>(key))) {
            _cache[key] = value;
            _dirty = true;
        }
    }
    private void Put<T>(SettingsKey key, T value) {
        Put(key.ToString(), value);
    }

    public async Task<T?> GetAsync<T>(SettingsKey key) {
        await InitializeAsync();
        return Get<T>(key);
    }

    public async Task PutAsync<T>(SettingsKey key, T value) {
        await InitializeAsync();
        Put(key, value);
        await CommitAsync();
    }

    public async Task EditAsync(Func<IUserSettingsAccessor, bool> fn) {
        await InitializeAsync();
        if (fn(new SettingsEditor(this))) {
            await CommitAsync();
        }
    }
}
