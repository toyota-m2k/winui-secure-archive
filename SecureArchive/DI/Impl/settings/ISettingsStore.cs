using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI.Impl.settings {
    internal interface ISettingsStore {
        Task<T?> GetAsync<T>(string key);
        Task PutAsync<T>(string key, T value);
        Task DeleteAsync<T>(string key);
    }
}
