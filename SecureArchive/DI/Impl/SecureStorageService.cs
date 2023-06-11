using Microsoft.Extensions.Logging;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    public async Task<bool> SaveFile(string filePath, string ownerId, string? name, long originalDate, string? originalId, string? metaInfo, ProgressProc? progress) {
        var type = Path.GetExtension(filePath) ?? "*";
        var info = new FileInfo(filePath);
        var fileName = name ?? Path.GetFileName(filePath);
        using (var inStream = File.OpenRead(filePath)) {
            return await SaveStream(inStream, ownerId, fileName, info.Length, type, info.LastWriteTime.Ticks, originalId, metaInfo, progress);
        }
    }

    public async Task<bool> SaveStream(Stream inStream, string ownerId, string name, long size, string type, long originalDate, string? originalId, string? metaInfo, ProgressProc? progress) {
        var outFolder = await _fileStoreService.GetFolder();
        var outFilePath = Path.Combine(outFolder!, Guid.NewGuid().ToString());
        try {
            using (var outStream = File.OpenWrite(outFilePath)) {
                await _cryptoService.EncryptStreamAsync(inStream, outStream, progress);
            }
            _databaseService.EditEntry((entry) => {
                entry.Add("@Local", name, size, type, outFilePath, originalDate);
                return true;
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Encryption Error.");
            if (File.Exists(outFilePath)) {
                FileUtils.SafeDelete(outFilePath);
            }
        }
    }

}
