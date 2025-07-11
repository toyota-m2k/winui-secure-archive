using io.github.toyota32k.media;
using SecureArchive.Models.DB;
using SecureArchive.Models.DB.Accessor;
using SecureArchive.Utils;
using System.Security.Cryptography;
using static io.github.toyota32k.media.MovieFastStart;

namespace SecureArchive.DI.Impl;

internal class SecureStorageService : ISecureStorageService {
    private ICryptographyService _cryptoService;
    private IFileStoreService _fileStoreService;
    private IDatabaseService _databaseService;
    private ITaskQueueService _taskQueueService;
    private ICryptoStreamHandler _cryptoStreamHandler;
    //private IStatusNotificationService _statusNotificationService;
    private UtLog _logger;

    public SecureStorageService(
            ICryptographyService cryptographyService,
            IFileStoreService fileStoreService,
            IDatabaseService databaseService,
            ITaskQueueService taskQueueService,
            ICryptoStreamHandler cryptoStreamHandler
            //IStatusNotificationService statusNotificationService,
            ) {
        _cryptoService = cryptographyService;
        _fileStoreService = fileStoreService;   
        _databaseService = databaseService; 
        _taskQueueService = taskQueueService;
        _cryptoStreamHandler = cryptoStreamHandler;
        //_statusNotificationService = statusNotificationService;
        _logger = UtLog.Instance(typeof(SecureStorageService));
    }

    #region Write

    public bool IsRegistered(string ownerId, int slot, string originalId) {
        return null != _databaseService.Entries.GetByOriginalId(ownerId, slot, originalId);
    }
    public bool IsRegistered(string ownerId, int slot, string originalId, long lastModified) {
        var item = _databaseService.Entries.GetByOriginalId(ownerId, slot, originalId);
        if(item==null) return false;
        return item.LastModifiedDate >= lastModified;
    }

    public IList<FileEntry> GetList(string ownerId, int slot, Func<FileEntry, bool>? predicate) {
        return _databaseService.Entries.List(slot, (entry) => {
            return entry.OwnerId == ownerId && (predicate?.Invoke(entry) ?? true);
        }, true);
    }

    public async Task<FileEntry?> RegisterFile(string filePath, string ownerId, int slot, string? name, string originalId, long duration, string? metaInfo, ProgressProc? progress) {
        var type = Path.GetExtension(filePath) ?? "*";
        var info = new FileInfo(filePath);
        var fileName = name ?? Path.GetFileName(filePath);
        using (var inStream = File.OpenRead(filePath)) {
            return await Register(inStream, ownerId, slot, fileName, info.Length, type, info.LastWriteTime.Ticks, info.CreationTime.Ticks, originalId, duration, metaInfo, progress);
        }
    }

    public async Task<FileEntry?> Register(Stream inStream, string ownerId, int slot, string name, long size, string type, long lastModifiedDate, long creationDate, string originalId, long duration, string? metaInfo, ProgressProc? progress) {
        var outFolder = await _fileStoreService.GetFolder();
        var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        try {
            using (var outStream = File.OpenWrite(cryptedFilePath)) {
                await _cryptoService.EncryptStreamAsync(inStream, outStream, progress);
            }
            FileEntry entry = null!;
            _databaseService.EditEntry((entryList) => {
                entry = entryList.Add(ownerId, slot, name, size, type, cryptedFilePath, lastModifiedDate, creationDate, originalId, duration, metaInfo);
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

    private class EntryCreator : IEntryCreator {
        public Stream OutputStream => _cryptoStream;

        private CryptoStream _cryptoStream;
        private IDatabaseService _databaseService;
        private Stream _innerStream;
        private string _cryptedFilePath;
        private bool _completed = false;

        private string _ownerId;
        private int _slot;
        private string _originalId;
        private long _existingId;

        public EntryCreator(string ownerId, int slot, string originalId, FileEntry? entry, string cryptedFilePath, ICryptographyService cryptService, IDatabaseService databaseService) {
            _ownerId = ownerId;
            _slot = slot;
            _originalId = originalId;
            _existingId = entry?.Id ?? -1;

            _cryptedFilePath = cryptedFilePath;
            _databaseService = databaseService;
            _innerStream = File.OpenWrite(cryptedFilePath);
            _cryptoStream = cryptService.OpenStreamForEncryption(_innerStream);
        }

        public void Abort() {
            
        }

        public FileEntry Complete(string name, long size, string type_, long lastModifiedDate, long creationDate, long duration, string? metaInfo, IItemExtAttributes? extAttr) {
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
                    entry = entryList.Add(_ownerId, _slot, name, size, type, _cryptedFilePath, lastModifiedDate, creationDate, _originalId, duration, metaInfo, extAttr);
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

    public async Task<IEntryCreator?> CreateEntry(string ownerId, int slot, string originalId, bool overwrite) {
        //_databaseService.EditOwnerList(list => {
        //    return list.Add(ownerId, "remote", "unknown", 0);
        //});
        var entry = _databaseService.Entries.GetByOriginalId(ownerId, slot, originalId);
        if (entry!=null && !overwrite) {
            return null;
        }
        var outFolder = await _fileStoreService.GetFolder();
        var cryptedFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        return new EntryCreator(ownerId, slot, originalId, entry, cryptedFilePath, _cryptoService, _databaseService);
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
                    foreach (var e in entries.List(-1, false)) {
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

    /**
     * FileEntryの参照ファイルを削除する。
     * CryptoStreamPool から参照されている場合に、Deleteが IOException をスローするので、
     * 事前に CryptoStreamHandler#AbortStream を呼び出すが、ストリームを閉じてもしばらく、
     * ファイルが解放されない可能性があるので、時間をあけて、５回リトライする。
     */
    private async Task InternalDeleteEntry(FileEntry entry) {
        int retry = 0;
        _cryptoStreamHandler.AbortStream(entry, true);
        while (true) {
            try {
                File.Delete(entry.Path);
                return;
            } catch(IOException e) {
                if (retry >= 4) {
                    throw new Exception($"cannot delete {entry.Path}", e);
                }
                await Task.Delay(100+retry*500);
                retry++;
            }
        }
    }


    public async Task<bool> DeleteEntry(FileEntry entry, bool deleteDbEntry) {
        return await Task.Run(async () => {
            try {
                await InternalDeleteEntry(entry);
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
                entry.Duration = GetLong(newValues, "duration", entry.Duration);
                entry.Slot = GetInt(newValues, "slot", entry.Slot);
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
        } catch(Exception e) {
            _logger.Error(e);
            return false;
        }
    }

    private class OutputFileSource : IOutputStreamFactory {
        private ICryptographyService _cryptoService;
        private string _path;

        public OutputFileSource(ICryptographyService cryptService, string path) {
            _cryptoService = cryptService;
            _path = path;
        }
        public Stream Create() {
            var outStream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            return _cryptoService.OpenStreamForEncryption(outStream);
        }
        public void Delete() {
            FileUtils.SafeDelete(_path);
        }
    }

    private class Notifier : INotify, IDisposable {
        IProgressHandle _progressHandle;
        UtLog _logger;
        public Notifier(IStatusNotificationService notificationService) {
            _progressHandle = notificationService.BeginProgress("FastStart");
            _logger = UtLog.Instance(typeof(Notifier));
        }
        public void Error(string message) {
            _progressHandle.UpdateMessage(message);
        }

        public void Message(string message) {
            _progressHandle.UpdateMessage(message);
        }

        public void UpdateProgress(long current, long total, string? message) {
            if (message != null) {
                _progressHandle.UpdateMessage(message);
            }
            _progressHandle.UpdateProgress(current, total);
        }

        public void Verbose(string message) {
            _logger.Debug(message);
        }

        public void Warning(string message) {
            _progressHandle.UpdateMessage(message);
        }

        public void Dispose() {
            _progressHandle.Dispose();
        }
    }

    private async Task<bool> ConvertFastStartCore(FileEntry entry, IStatusNotificationService? notificationService, bool onlyCheck) {
        bool success = false;
        var backupPath = entry.Path + ".bak";
        var workingPath = entry.Path + ".mfs";
        if (File.Exists(backupPath)) {
            // 既に*.bakファイルが存在する場合は、何もしない。
            // notificationService?.ShowMessage("Already converted.", 5000);
            if (File.Exists(entry.Path)) {
                // 元ファイルが存在するなら *.bakは削除
                FileUtils.SafeDelete(backupPath);
            }
            else {
                notificationService?.ShowMessage($"Aborted: {entry.Path}", 15000);
                return false;
            }
        }
        if(File.Exists(workingPath)) {
            // 既に*.mfsファイルが存在する場合は、削除する
            FileUtils.SafeDelete(workingPath);
        }

        try {
            using var notifier = notificationService != null ? new Notifier(notificationService) : null;
            using var inputStream = new SeekableInputStream(oldStream => {
                oldStream?.Dispose();
                return _cryptoService.OpenStreamForDecryption(File.OpenRead(entry.Path));
            });
            var fs = new MovieFastStart() {
                TaskName = entry.Name,
                Notify = notifier
            };
            if (onlyCheck) {
                await fs.Check(inputStream);
                if (fs.SourceStatus.AlreadySuitable) {
                    notificationService?.ShowMessage($"Already suitable: {entry.Name}", 5000);
                    return false;
                }
                else if (fs.SourceStatus.Unsupported) {
                    notificationService?.ShowMessage($"Unsupported: {entry.Name}", 5000);
                    return false;
                }
                else {
                    notificationService?.ShowMessage($"Need to convert: {entry.Name}", 5000);
                    return true;
                }
            }
            var output = new OutputFileSource(_cryptoService, workingPath);
            success = await fs.Process(inputStream, output);
            if (success) {
                // input file を閉じる
                inputStream.Dispose();

                // 成功すれば、ファイル、データベースを更新
                // まず、元のファイルを *.bak にリネームして退避
                if (!RenameFile(entry.Path, backupPath)) {
                    // オリジナルファイルをリネームできない場合は、FastStartファイルを削除して終了
                    notificationService?.ShowMessage("Cannot rename original file.", 5000);
                    output.Delete();
                    return false;
                }
                // FastStartファイルを元のファイル名にリネーム
                if (!RenameFile(workingPath, entry.Path)) {
                    // FastStartファイルをリネームできない場合は、オリジナルファイルを戻して終了
                    notificationService?.ShowMessage("Cannot rename FastStart file.", 5000);
                    if (!RenameFile(backupPath, entry.Path)) {
                        // fatal error.
                        notificationService?.ShowMessage("Fatal Error: Cannot restore original file.", 5000);
                    }
                    return false;
                }
                // *.bak を削除
                FileUtils.SafeDelete(backupPath);

                _databaseService.EditEntry((entryList) => {
                    // Overwrite
                    var mutableEntry = entryList.GetById(entry.Id)!;
                    mutableEntry.Size = fs.OutputLength;
                    mutableEntry.LastModifiedDate = TimeUtils.dateTime2javaTime(DateTime.Now);
                    return true;
                });
            }
            else {
                // 失敗した場合は、FastStartファイルを削除して終了
                // MovieFastStartが削除しているはずだが、念のため。
                output.Delete();
            }
            return success;
            
        } catch (Exception e) {
            _logger.Error(e);
            return false;
        }
    }
    private async Task<bool> ConvertFastStart(FileEntry entry, IStatusNotificationService? notificationService, bool onlyCheck) {
        return await Task.Run(() => {
            return ConvertFastStartCore(entry, notificationService, onlyCheck);
        });
    }

    /**
     * すべてのmp4ファイルをFastStartに変換する。
     */
    public async Task ConvertFastStart(IStatusNotificationService notificationService, List<FileEntry> entries) {
        entries = entries.Where(e => e.Type == "mp4").ToList();
        if (entries.Count == 0) {
            _logger.Info("No mp4 files.");
            return;
        }

        if (entries.Count == 1) {
            if(!await ConvertFastStart(entries[0], notificationService, onlyCheck: false)) {
                _logger.Error($"not converted: {entries[0].Name}");
            }
        }
        else {
            await notificationService.WithProgress("FastStart", async (message, progress) => {
                for (int i = 0; i < entries.Count; i++) {
                    var entry = entries[i];
                    message($"Processing {entry.Name}");
                    progress(i + 1, entries.Count);
                    if (!await ConvertFastStart(entries[i], null, onlyCheck: false)) {
                        _logger.Error($"not converted: {entries[0].Name}");
                    }
                }
                message($"FastStart: completed.");
            });
        }
    }
    public async Task CheckFastStart(IStatusNotificationService notificationService, List<FileEntry> entries) {
        entries = entries.Where(e => e.Type == "mp4").ToList();
        if (entries.Count == 0) {
            _logger.Info("No mp4 files.");
            return;
        }

        if (entries.Count == 1) {
            if(await ConvertFastStart(entries[0], notificationService, onlyCheck: true)) {
                _logger.Debug($"need to convert: {entries[0].Name}:{entries[0].Path}");
            }
        }
        else {
            var slow = new List<FileEntry>();
            await notificationService.WithProgress("FastStart", async (message, progress) => {
                for (int i = 0; i < entries.Count; i++) {
                    var entry = entries[i];
                    message($"Processing {entry.Name}");
                    progress(i + 1, entries.Count);
                    if (await ConvertFastStart(entries[i], null, onlyCheck: true)) {
                        slow.Add(entry);
                    }
                }
                if (slow.Any()) {
                    foreach (var e in slow) {
                        _logger.Debug($"need to convert: {e.Name}:{e.Path}");
                    }
                }
                message($"FastStart: completed.");
            });
        }
    }
}
