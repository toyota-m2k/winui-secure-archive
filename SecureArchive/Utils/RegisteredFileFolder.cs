using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace SecureArchive.Utils {
    internal class RegisteredFileFolder {
        private string _token;
        public RegisteredFileFolder(string token) {
            _token = token;
        }
        public bool Registered => IsRegistered(_token);
        public bool Register(StorageFolder path, bool replaceIfExist=false) => Register(_token, path, replaceIfExist);
        public bool Unregister() => Unregister(_token);
        public Task<StorageFolder?> Folder => Retrieve(_token);
        

        public static bool IsRegistered(string token) {
            return StorageApplicationPermissions.FutureAccessList.ContainsItem(token);
        }
        public static bool Register(string token, StorageFolder path, bool replaceIfExist=false) {
            if(!replaceIfExist && IsRegistered(token)) {
                return false;
            }
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, path);
            return true;
        }

        public static bool Unregister(string token) {
            if(!IsRegistered(token)) { return false; }
            StorageApplicationPermissions.FutureAccessList.Remove(token);
            return true;
        }

        public static async Task<StorageFolder?> Retrieve(string token) {
            return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }
    }
}
