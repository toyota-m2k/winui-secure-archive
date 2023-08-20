namespace SecureArchive.DI;
internal interface IFileStoreService {
    Task<bool> IsReady();
    Task SetFolder(string newFolder);
    Task<string?> GetFolder();

    //Task<bool> Register(StorageFolder newFolder);
    //Task<StorageFolder?> GetFolder();
}
