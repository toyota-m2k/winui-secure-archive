using Windows.Storage;

namespace SecureArchive.DI;
internal interface IFileStoreService {
    Task<bool> IsRegistered();
    Task<bool> Register(string newFolder);
    Task<string?> GetFolder();

    //Task<bool> Register(StorageFolder newFolder);
    //Task<StorageFolder?> GetFolder();
}
