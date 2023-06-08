using SecureArchive.Utils;
using Windows.Storage;

namespace SecureArchive.DI.Impl {
    internal class FileStoreService:IFileStoreService {
        IUserSettingsService _userSettingsService;
        public FileStoreService(IUserSettingsService userSettingsService) {
            _userSettingsService = userSettingsService;
        }


        public async Task<bool> IsRegistered() {
            var path = await _userSettingsService.GetAsync<string>(SettingsKey.DataFolder);
            if(string.IsNullOrEmpty(path)) return false;
            return Path.Exists(path);
        }

        public async Task<bool> Register(string newFolder) {
            // 新しいフォルダに読み書きできることを確認
            try {
                var checkFile = Path.Combine(newFolder, "a.txt");
                File.WriteAllText(checkFile, "abcdefg");
                if (!File.Exists(checkFile)) {
                    return false;
                }
                File.Delete(checkFile);
            } catch(Exception) {
                // 読み書きできないっぽい。
                return false;
            }

            var oldFolder = await GetFolder();
            if(oldFolder != null && Path.Exists(oldFolder)) {
                if (!FileUtils.IsFolderEmpty(oldFolder)) {
                    await FileUtils.MoveItemsInFolder(oldFolder, newFolder);
                }
                Directory.Delete(oldFolder, true);
            }
            await _userSettingsService.PutAsync<string>(SettingsKey.DataFolder, newFolder);
            return true;
        }

        public async Task<string?> GetFolder() {
            var folder = await _userSettingsService.GetAsync<string>(SettingsKey.DataFolder);
            if (folder == null) return null;
            if (!Path.Exists(folder)) {
                return null;
            }
            return folder;
        }

    }

    internal class FileStoreServiceWithRegisteredFileFolder {
        const string TOKEN_DATA_FOLDER = "SA.DataFolder";
        private RegisteredFileFolder _fileFolder = new(TOKEN_DATA_FOLDER);

        public bool IsRegistered => _fileFolder.Registered;

        public async Task<bool> Register(StorageFolder newFolder) {
            if(IsRegistered) {
                var oldFolder = await GetFolder();
                if(oldFolder != null && await FileUtils.IsEmpty(oldFolder)) {
                    await FileUtils.MoveItemsInFolder(oldFolder, newFolder);
                }
            }
            return _fileFolder.Register(newFolder, replaceIfExist:true);
        }

        public async Task<StorageFolder?> GetFolder() {
            if (IsRegistered) {
                return await _fileFolder.Folder;
            } else {
                return null;
            }
        }
        
    }
}
