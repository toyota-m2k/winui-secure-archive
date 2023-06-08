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
    Task<T?> GetAsync<T>(SettingsKey key);
    Task PutAsync<T>(SettingsKey key, T value);
    Task EditAsync(Func<IUserSettingsAccessor, bool> editor);
}