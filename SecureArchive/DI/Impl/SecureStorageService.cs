using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using System.Security.Cryptography;
using Windows.Storage.Streams;

namespace SecureArchive.DI.Impl;

internal class SecureStorageService : ISecureStorageService {
    private ICryptographyService _cryptoService;
    private IFileStoreService _fileStoreService;
    private IDatabaseService _databaseService;
    private ITaskQueueService _taskQueueService;
    private IStatusNotificationService _statusNotificationService;
    private ILogger _logger;

    public SecureStorageService(
            ICryptographyService cryptographyService,
            IFileStoreService fileStoreService,
            IDatabaseService databaseService,
            ITaskQueueService taskQueueService,
            IStatusNotificationService statusNotificationService,
            ILoggerFactory loggerFactory) {
        _cryptoService = cryptographyService;
        _fileStoreService = fileStoreService;   
        _databaseService = databaseService; 
        _taskQueueService = taskQueueService;
        _statusNotificationService = statusNotificationService;
        _logger = loggerFactory.CreateLogger<SecureStorageService>();
    }

    #region Write

    public bool IsRegistered(string ownerId, string originalId) {
        return null != _databaseService.Entries.GetByOriginalId(ownerId, originalId);
    }
    public bool IsRegistered(string ownerId, string originalId, long lastModified) {
        var item = _databaseService.Entries.GetByOriginalId(ownerId, originalId);
        if(item==null) return false;
        return item.OriginalDate >= lastModified;
    }

    public async Task<FileEntry?> RegisterFile(string filePath, string ownerId, string? name, string originalId, string? metaInfo, ProgressProc? progress) {
        var type = Path.GetExtension(filePath) ?? "*";
        var info = new FileInfo(filePath);
        var fileName = name ?? Path.GetFileName(filePath);
        using (var inStream = File.OpenRead(filePath)) {
            return await Register(inStream, ownerId, fileName, info.Length, type, info.LastWriteTime.Ticks, info.CreationTime.Ticks, originalId, metaInfo, progress);
        }
    }

    public async Task<FileEntry?> Register(Stream inStream, string ownerId, string name, long size, string type, long originalDate, long creationDate, string originalId, string? metaInfo, ProgressProc? progress) {
        var outFolder = await _fileStoreService.GetFolder();
        var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        try {
            using (var outStream = File.OpenWrite(cryptedFilePath)) {
                await _cryptoService.EncryptStreamAsync(inStream, outStream, progress);
            }
            FileEntry entry = null!;
            _databaseService.EditEntry((entryList) => {
                entry = entryList.Add(ownerId, name, size, type, cryptedFilePath, originalDate, creationDate, originalId, metaInfo);
                return true;
            });
            return entry;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Encryption Error.");
            if (File.Exists(cryptedFilePath)) {
                FileUtils.SafeDelete(cryptedFilePath);
            }
            return null;
        }
    }

    //public async Task<FileEntry?> CreateEntry(Func<Stream, bool> writer, string ownerId, string name, long size, string type, long originalDate, string? originalId, string? metaInfo) {
    //    var outFolder = await _fileStoreService.GetFolder();
    //    var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
    //    try {
    //        using (var outStream = File.OpenWrite(cryptedFilePath)) {
    //            if(!_cryptoService.OpenStreamForEncryption(outStream, writer)) {
    //                throw new OperationCanceledException("cancelled.");
    //            }
    //        }
    //        FileEntry? entry = null;
    //        _databaseService.EditEntry((entryList) => {
    //            entry = entryList.Add(ownerId, name, size, type, cryptedFilePath, originalDate, originalId, metaInfo);
    //            return true;
    //        });
    //        return entry;
    //    }
    //    catch (Exception) {
    //        FileUtils.SafeDelete(cryptedFilePath);
    //        return null;
    //    }
    //}

    private class EntryCreator : IEntryCreator {
        public Stream OutputStream => _cryptoStream;

        private CryptoStream _cryptoStream;
        private IDatabaseService _databaseService;
        private Stream _innerStream;
        private string _cryptedFilePath;
        private bool _completed = false;

        private string _ownerId;
        private string _originalId;
        private long _existingId;

        public EntryCreator(string ownerId, string originalId, FileEntry? entry, string cryptedFilePath, ICryptographyService cryptService, IDatabaseService databaseService) {
            _ownerId = ownerId;
            _originalId = originalId;
            _existingId = entry?.Id ?? -1;

            _cryptedFilePath = cryptedFilePath;
            _databaseService = databaseService;
            _innerStream = File.OpenWrite(cryptedFilePath);
            _cryptoStream = cryptService.OpenStreamForEncryption(_innerStream);
        }

        public void Abort() {
            
        }

        public FileEntry Complete(string name, long size, string type_, long originalDate, long creationDate, string? metaInfo) {
            _completed = true;
            _cryptoStream.FlushFinalBlock();
            _cryptoStream.Flush();
            _cryptoStream.Dispose();
            _innerStream.Dispose();

            var type = type_;
            if(type_.StartsWith(".")) {
                type = type_.Substring(1);
            }
            FileEntry entry = null!;
            _databaseService.EditEntry((entryList) => {
                if (_existingId >= 0) {
                    // Overwrite
                    entry = entryList.GetById(_existingId)!;
                    FileUtils.SafeDelete(entry.Path);
                    entry.Path = _cryptedFilePath;
                    entry.Name = name;
                    entry.Size = size;
                    entry.Type = type;
                    entry.OriginalDate = originalDate;
                    entry.MetaInfo = metaInfo;
                    return true;
                }
                else {
                    // New entry.
                    entry = entryList.Add(_ownerId, name, size, type, _cryptedFilePath, originalDate, creationDate, _originalId, metaInfo);
                    return true;
                }
            });
            return entry;
        }

        public void Dispose() {
            if(!_completed) {
                _cryptoStream.Dispose();
                _innerStream.Dispose();
                FileUtils.SafeDelete(_cryptedFilePath);
            }
        }
    }

    public async Task<IEntryCreator?> CreateEntry(string ownerId, string originalId, bool overwrite) {
        _databaseService.EditOwnerList(list => {
            return list.Add(ownerId, "remote", "unknown", 0);
        });
        var entry = _databaseService.Entries.GetByOriginalId(ownerId, originalId);
        if (entry!=null && !overwrite) {
            return null;
        }
        var outFolder = await _fileStoreService.GetFolder();
        var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        return new EntryCreator(ownerId, originalId, entry, cryptedFilePath, _cryptoService, _databaseService);
    }

    #endregion
    #region Read

    public Stream OpenEntry(FileEntry entry) {
        var cryptedStream = File.OpenRead(entry.Path);
        return _cryptoService.OpenStreamForDecryption(cryptedStream);
    }

    public async Task Export(FileEntry entry, string outPath) {
        await Task.Run(() => {
            using (var outStream = File.OpenWrite(outPath))
            using(var inStream =OpenEntry(entry)) {
                inStream.CopyTo(outStream);
            }
        });
    }
    public async Task Export(FileEntry entry, string outPath, ProgressProc progress) {
        await Task.Run(() => {
            using (var outStream = File.OpenWrite(outPath))
            using (var inStream = OpenEntry(entry)) {
                var buffer = new byte[4096];
                int len;
                long total = entry.Size;
                long current = 0;
                while (true) {
                    len = inStream.Read(buffer, 0, buffer.Length);
                    if (len <= 0) break;
                    outStream.Write(buffer, 0, len);
                    current += len;
                    progress?.Invoke(current, total);
                }
                //cryptoStream.FlushFinalBlock();
                outStream.Flush();
            }
        });
    }

    #endregion
}
