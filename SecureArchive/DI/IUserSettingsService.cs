using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal enum SettingsKey {
    DataFolder,
    PortNo,
}

internal interface IUserSettingsAccessor {
    string? DataFolder { get; set; }
    int PortNo { get; set; }
}

internal interface IUserSettingsService {
    Task<string?> GetStringAsync(SettingsKey key);
    Task<int> GetIntAsync(SettingsKey key, int defaultValue = 0);
    Task<long> GetLongAsync(SettingsKey key, long defaultValue = 0);
    Task PutAsync<T>(SettingsKey key, T value);
    Task EditAsync(Func<IUserSettingsAccessor, bool> editor);
}