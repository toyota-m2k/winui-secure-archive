using Microsoft.Extensions.Logging;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Xml.Linq;
using Windows.Storage.Streams;

namespace SecureArchive.DI.Impl;

internal class SecureStorageService : ISecureStorageService {
    private ICryptographyService _cryptoService;
    private IFileStoreService _fileStoreService;
    private IDatabaseService _databaseService;
    private ITaskQueueService _taskQueueService;
    //private IStatusNotificationService _statusNotificationService;
    private ILogger _logger;

    public SecureStorageService(
            ICryptographyService cryptographyService,
            IFileStoreService fileStoreService,
            IDatabaseService databaseService,
            ITaskQueueService taskQueueService,
            //IStatusNotificationService statusNotificationService,
            ILoggerFactory loggerFactory) {
        _cryptoService = cryptographyService;
        _fileStoreService = fileStoreService;   
        _databaseService = databaseService; 
        _taskQueueService = taskQueueService;
        //_statusNotificationService = statusNotificationService;
        _logger = loggerFactory.CreateLogger<SecureStorageService>();
    }

    #region Write

    public bool IsRegistered(string ownerId, string originalId) {
        return null != _databaseService.Entries.GetByOriginalId(ownerId, originalId);
    }
    public bool IsRegistered(string ownerId, string originalId, long lastModified) {
        var item = _databaseService.Entries.GetByOriginalId(ownerId, originalId);
        if(item==null) return false;
        return item.LastModifiedDate >= lastModified;
    }

    public IList<FileEntry> GetList(string ownerId, Func<FileEntry, bool>? predicate) {
        return _databaseService.Entries.List((entry) => {
            return entry.OwnerId == ownerId && (predicate?.Invoke(entry) ?? true);
        }, true);
    }

    public async Task<FileEntry?> RegisterFile(string filePath, string ownerId, string? name, string originalId, string? metaInfo, ProgressProc? progress) {
        var type = Path.GetExtension(filePath) ?? "*";
        var info = new FileInfo(filePath);
        var fileName = name ?? Path.GetFileName(filePath);
        using (var inStream = File.OpenRead(filePath)) {
            return await Register(inStream, ownerId, fileName, info.Length, type, info.LastWriteTime.Ticks, info.CreationTime.Ticks, originalId, metaInfo, progress);
        }
    }

    public async Task<FileEntry?> Register(Stream inStream, string ownerId, string name, long size, string type, long lastModifiedDate, long creationDate, string originalId, string? metaInfo, ProgressProc? progress) {
        var outFolder = await _fileStoreService.GetFolder();
        var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        try {
            using (var outStream = File.OpenWrite(cryptedFilePath)) {
                await _cryptoService.EncryptStreamAsync(inStream, outStream, progress);
            }
            FileEntry entry = null!;
            _databaseService.EditEntry((entryList) => {
                entry = entryList.Add(ownerId, name, size, type, cryptedFilePath, lastModifiedDate, creationDate, originalId, metaInfo);
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

    //public async Task<FileEntry?> CreateEntry(Func<Stream, bool> writer, string ownerId, string name, long size, string type, long lastModifiedDate, string? originalId, string? metaInfo) {
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
    //            entry = entryList.Add(ownerId, name, size, type, cryptedFilePath, lastModifiedDate, originalId, metaInfo);
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

        public FileEntry Complete(string name, long size, string type_, long lastModifiedDate, long creationDate, string? metaInfo, IItemExtAttributes? extAttr) {
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
                    entry.LastModifiedDate = lastModifiedDate;
                    entry.MetaInfo = metaInfo;
                    if(extAttr!=null) {
                        entry.ExtAttrDate = extAttr.ExtAttrDate;
                        entry.Rating = extAttr.Rating;
                        entry.Mark = extAttr.Mark;
                        entry.Category = extAttr.Category;
                        entry.Chapters = extAttr.Chapters;
                    }
                    return true;
                }
                else {
                    // New entry.
                    entry = entryList.Add(_ownerId, name, size, type, _cryptedFilePath, lastModifiedDate, creationDate, _originalId, metaInfo, extAttr);
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

    public async Task<bool> SetStorageFolder(string newPath) {
        if (!Directory.Exists(newPath)) {
            _logger.Error($"\"{newPath}\" is not exists.");
            return false;
        }
        //if (!FileUtils.IsFolderEmpty(newPath)) {
        //    _logger.Error($"\"{newPath}\" is not empty.");
        //    return false;
        //}
        // 新しいフォルダに読み書きできることを確認
        try {
            var checkFile = Path.Combine(newPath, "a.txt");
            File.WriteAllText(checkFile, "abcdefg");
            if (!File.Exists(checkFile)) {
                throw new Exception("file error");
            }
            File.Delete(checkFile);
        }
        catch (Exception) {
            // 読み書きできないっぽい。
            _logger.Error($"no file can be created in \"{newPath}\".");
            return false;
        }

        var oldPath = await _fileStoreService.GetFolder();
        if(oldPath != null && Directory.Exists(oldPath) && !FileUtils.IsFolderEmpty(oldPath)) {
            if(!await MoveContents(oldPath, newPath)) {
                _logger.Error("Cannot move contents to new folder.");
                return false;
            }
        }
        await _fileStoreService.SetFolder(newPath);
        return true;
    }

    private async Task<bool> MoveContents(string from, string to) {
        return await Task.Run(() => {
            try {
                if(from.EndsWith("\\")) {
                    from = from.Substring(0, from.Length - 1);
                }
                _databaseService.EditEntry(entries => {
                    bool result = false;
                    foreach (var e in entries.List(false)) {
                        var dir = Path.GetDirectoryName(e.Path);
                        var name = Path.GetFileName(e.Path);
                        if (dir == null || name == null) {
                            _logger.Info($"Invalid Path: {e.Path} (id={e.Id})");
                            continue;
                        }
                        if (string.Compare(from, dir, StringComparison.OrdinalIgnoreCase) == 0) {
                            // needs to move.
                            result = true;
                            var dstPath = Path.Combine(to, name);
                            if (File.Exists(dstPath)) {
                                // コピー先ファイルが存在する場合は、リトライ中とみなして、コピーはスキップ
                                _logger.Info($"File already exists: {dstPath}");
                                // DBのパスは更新
                                e.Path = dstPath;
                                continue;
                            }
                            if (!File.Exists(e.Path)) {
                                // コピー元ファイルが存在しない場合は、エントリを削除する。
                                _logger.Info($"Source file not found: {e.Path}");
                                entries.Remove(e, true);
                                continue;
                            }
                            // エントリをコピー
                            File.Copy(e.Path, dstPath);
                            _logger.Debug($"Moved: {e.Path} --> {dstPath}");
                            e.Path = dstPath;
                        }
                    }
                    return result;
                });

                //_logger.Debug("Removing Original Folder...");
                //await FileUtils.DeleteFolder(from);
                //_logger.Debug("Removed...");
                _logger.Info("All contents moved.");
                return true;
            }
            catch (Exception ex) {
                _logger.Error(ex);
                return false;
            }
        });
    }

    public async Task<bool> DeleteEntry(FileEntry entry, bool deleteDbEntry) {
        return await Task.Run(() => {
            try {
                File.Delete(entry.Path);
                _databaseService.EditEntry(entries => {
                    entries.Remove(entry, deleteDbEntry);
                    return true;
                });
                return true;
            }
            catch (Exception ex) {
                _logger.Error(ex);
                return false;
            }
        });
    }

    static public string GetString(IDictionary<string,object> dic, string key, string defValue) {
        if (dic.TryGetValue(key, out var value)) {
            return value?.ToString() ?? defValue;
        }
        return defValue;
    }
    static public string? GetNullableString(IDictionary<string, object> dic, string key, string? defValue) {
        if (dic.TryGetValue(key, out var value)) {
            return value?.ToString();
        }
        return defValue;
    }
    static public long GetLong(IDictionary<string, object> dic, string key, long defValue) {
        if (dic.TryGetValue(key, out var value)) {
              if(value is long l) {
                return l;
            }
            if(value is int i) {
                return i;
            }
            if(value is string s) {
                if(long.TryParse(s, out var l2)) {
                    return l2;
                }
            }
        }
        return defValue;
    }
    static public int GetInt(IDictionary<string, object> dic, string key, int defValue) {
        return (int)GetLong(dic, key, defValue);
    }

    public bool UpdateEntry(
        long id,
        Dictionary<string, object> newValues
        ) {
        try {
            _databaseService.EditEntry((entryList) => {
                // Overwrite
                var entry = entryList.GetById(id)!;
                entry.OriginalId = GetString(newValues, "originalId", entry.OriginalId);
                entry.OwnerId = GetString(newValues, "ownerId", entry.OwnerId);

                entry.Name = GetString(newValues, "name", entry.Name);
                entry.Size = GetLong(newValues, "size", entry.Size);
                entry.Type = GetString(newValues, "type", entry.Type);
                //entry.Path = entry.Path;
                entry.RegisteredDate = entry.RegisteredDate;
                entry.LastModifiedDate = GetLong(newValues, "lastModifiedDate", entry.LastModifiedDate);
                entry.CreationDate = GetLong(newValues, "creationDate", entry.CreationDate);
                entry.MetaInfo = GetNullableString(newValues, "metaInfo", entry.MetaInfo);
                entry.Deleted = GetLong(newValues, "deleted", entry.Deleted);
                entry.ExtAttrDate = GetLong(newValues, "extAttrDate", entry.ExtAttrDate);
                entry.Rating = GetInt(newValues, "rating", entry.Rating);
                entry.Mark = GetInt(newValues, "mark", entry.Mark);
                entry.Category = GetNullableString(newValues, "category", entry.Category);
                entry.Chapters = GetNullableString(newValues, "chapters", entry.Chapters);
                return true;
            });
            return true;
        }
        catch (Exception ex) {
            _logger.Error(ex);
            return false;
        }
    }

    private bool RenameFile(string src, string dst) {
        try {
            File.Move(src, dst);
            return true;
        } catch(Exception) {
            return false;
        }
    }

    private async Task<bool> ConvertFastStart(FileEntry entry, IStatusNotificationService? notificationService) {
        bool success = false;
        string backupPath = entry.Path + ".bak";
        if(!RenameFile(entry.Path, backupPath)) {
            return false;
        }

        try {
            var srcStream = _cryptoService.OpenStreamForDecryption(File.OpenRead(backupPath));

            using (var inputStream = new SeekableInputStream(srcStream, (oldStream) => {
                oldStream.Dispose();
                return _cryptoService.OpenStreamForDecryption(File.OpenRead(backupPath));
            })) {
                var fs = new MovieFastStart() { TaskName = entry.Name };
                success = await fs.Process(inputStream, () => {
                    var outStream = new FileStream(entry.Path, FileMode.Create, FileAccess.Write, FileShare.None);
                    return _cryptoService.OpenStreamForEncryption(outStream);
                }, notificationService);
                if (success) {
                    // 成功すれば、データベースを更新。
                    _databaseService.EditEntry((entryList) => {
                        // Overwrite
                        var mutableEntry = entryList.GetById(entry.Id)!;
                        mutableEntry.Size = fs.OutputLength;
                        return true;
                    });
                }
                return success;
            }
        } catch (Exception e) {
            _logger.Error(e);
            return false;
        } finally {
            if (!success) {
                // 失敗したら元に戻す。
                _logger.Error($"Error: {entry.Name}");
                RenameFile(backupPath, entry.Path);
            }
        }
    }

    /**
     * すべてのmp4ファイルをFastStartに変換する。
     */
    public async Task ConvertFastStart(IStatusNotificationService? notificationService) {
        var entries = _databaseService.Entries.List((entry) => {
            return entry.Type == "mp4";
        }, true);
        //var entry = entries.FirstOrDefault();
        //if (entry == null) {
        //    _logger.Info("No mp4 files.");
        //    return;
        //}
        //_logger.Debug(entry.Name);
        //_logger.Debug(entry.Path);
        //await ConvertFastStart(entry);

        foreach (var entry in entries) {
            await ConvertFastStart(entry, notificationService);
        }
    }

    public async Task CheckItemLength() {

    }
}
