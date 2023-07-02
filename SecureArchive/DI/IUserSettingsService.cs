using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal enum SettingsKey {
    DataFolder,
    PortNo,
    ServerAutoStart,
}

internal interface IReadonlyUserSettingsAccessor {
    string? DataFolder { get; }
    int PortNo { get; }
    bool ServerAutoStart { get; }
}
internal interface IUserSettingsAccessor : IReadonlyUserSettingsAccessor {
    new string? DataFolder { get; set; }
    new int PortNo { get; set; }
    new bool ServerAutoStart { get; set; }
}

internal interface IUserSettingsService {
    //Task<string?> GetStringAsync(SettingsKey key);
    //Task<int> GetIntAsync(SettingsKey key, int defaultValue = 0);
    //Task<long> GetLongAsync(SettingsKey key, long defaultValue = 0);
    //Task PutAsync<T>(SettingsKey key, T value);
    Task EditAsync(Func<IUserSettingsAccessor, bool> editor);
    Task<IReadonlyUserSettingsAccessor> GetAsync();
}