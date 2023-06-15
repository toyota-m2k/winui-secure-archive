using System.CodeDom;
using System.Diagnostics;
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
            get => _userSettings.GetString(callerName());
            set => _userSettings.Put(callerName(), value);
        }
        public int PortNo { 
            get => _userSettings.GetInt(callerName(), 6001);
            set => _userSettings.Put<int>(callerName(), value);
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
    //private T? Get<T>(string key, T? defalutValue = default) {
    //    if (_cache == null) {
    //        throw new InvalidOperationException("call InitializeAsync() in prior.");
    //    }
    //    if (_cache.TryGetValue(key, out var value)) {
    //        return (T)value;
    //    }
    //    else {
    //        return defalutValue;
    //    }
    //}

    //private object? Get(string key) {
    //    if (_cache == null) {
    //        throw new InvalidOperationException("call InitializeAsync() in prior.");
    //    }
    //    if (_cache.TryGetValue(key, out var value)) {
    //        return value;
    //    }
    //    return null;
    //}

    private string? GetString(string key, string? defalutValue = default) {
        if (_cache == null) {
            throw new InvalidOperationException("call InitializeAsync() in prior.");
        }
        if (_cache.TryGetValue(key, out var value)) {
            return Convert.ToString(value);
        }
        else {
            return defalutValue;
        }
    }
    private int GetInt(string key, int defalutValue = 0) {
        if (_cache == null) {
            throw new InvalidOperationException("call InitializeAsync() in prior.");
        }
        if (_cache.TryGetValue(key, out var value)) {
            return Convert.ToInt32(value);
        }
        else {
            return defalutValue;
        }
    }
    private long GetLong(string key, long defalutValue = 0) {
        if (_cache == null) {
            throw new InvalidOperationException("call InitializeAsync() in prior.");
        }
        if (_cache.TryGetValue(key, out var value)) {
            return Convert.ToInt64(value);
        }
        else {
            return defalutValue;
        }
    }

    //private T? Get<T>(SettingsKey key) {
    //    return Get<T>(key.ToString());
    //}

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
        bool needUpdate;
        switch(value) {
            case int i:
                needUpdate = i != GetInt(key);
                break;
            case long l:
                needUpdate = l != GetLong(key);
                break;
            case string s:
                needUpdate = s != GetString(key);
                break;
            default:
                Debug.Assert(false, $"unknown type: {value.GetType()}");
                return;
        }
        if(needUpdate) { 
            _cache[key] = value;
            _dirty = true;
        }
    }
    private void Put<T>(SettingsKey key, T value) {
        Put(key.ToString(), value);
    }

    public async Task<string?> GetStringAsync(SettingsKey key) {
        await InitializeAsync();
        return GetString(key.ToString());
    }
    public async Task<int> GetIntAsync(SettingsKey key, int defaultValue=0) {
        await InitializeAsync();
        return GetInt(key.ToString(),defaultValue);
    }
    public async Task<long> GetLongAsync(SettingsKey key, long defaultValue = 0) {
        await InitializeAsync();
        return GetLong(key.ToString(), defaultValue);
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
