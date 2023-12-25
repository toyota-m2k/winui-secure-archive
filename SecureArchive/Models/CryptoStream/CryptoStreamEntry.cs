using SecureArchive.DI;
using SecureArchive.Models.DB;
using SecureArchive.Utils;

namespace SecureArchive.Models.CryptoStream;
internal class CryptoStreamEntry : ICryptoStreamContainer, IDisposable
{
    static readonly long LifeTime = TimeSpan.FromMinutes(5).Ticks;      // 5 minutes
    public FileEntry FileEntry { get; }
    public Stream Stream { get; }
    public bool InUse { get; set; } = false;

    public CryptoStreamEntry(FileEntry entry)
    {
        FileEntry = entry;
        var secureStorageService = App.GetService<ISecureStorageService>();
        Stream = new SeekableInputStream(secureStorageService.OpenEntry(entry), reopenStreamProc: (oldStream) =>
        {
            oldStream.Dispose();
            return secureStorageService.OpenEntry(entry);
        });
    }

    public void Dispose()
    {
        Stream.Dispose();
    }
}
