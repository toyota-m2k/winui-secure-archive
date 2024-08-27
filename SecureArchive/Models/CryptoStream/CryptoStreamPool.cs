using SecureArchive.Models.DB;
using SecureArchive.Utils;

namespace SecureArchive.Models.CryptoStream;
internal class CryptoStreamPool : IDisposable {
    private static readonly long LifeTime = TimeSpan.FromMinutes(5).Ticks;      // 5 minutes
    private static UtLog _logger = new(typeof(CryptoStreamPool));

    public FileEntry FileEntry { get; }
    public long LastAccess { get; private set; } = DateTime.Now.Ticks;
    private long Id { get; } // for debug
    public CryptoStreamPool(FileEntry fileEntry, long id) {
        FileEntry = fileEntry;
        Id = id;
    }

    private List<CryptoStreamEntry> _streamList = new();

    public ICryptoStreamContainer LockStream() {
        var entry = _streamList.FirstOrDefault((it) => !it.InUse);
        if (entry == null) {
            _logger.Debug($"Create new stream for {FileEntry.Name} ({_streamList.Count+1})");
            entry = new CryptoStreamEntry(FileEntry);
            _streamList.Add(entry);
        }
        entry.InUse = true;
        LastAccess = DateTime.Now.Ticks;
        return entry;
    }

    public void UnlockStream(ICryptoStreamContainer container) {
        var entry = container as CryptoStreamEntry;
        if (entry == null) {
            throw new ArgumentException("container is not a CryptoStreamEntry");
        }

        entry.InUse = false;
        LastAccess = DateTime.Now.Ticks;
    }

    public bool Sweep() {
        if (DateTime.Now.Ticks - LastAccess > LifeTime && !_streamList.Any((it) => it.InUse)) {
            _logger.Debug($"Sweeping: {FileEntry.Name}");
            Dispose();
            return true;
        }
        return false;
    }

    public void Dispose() {
        _logger.Debug($"Dispose: {FileEntry.Name}");
        _streamList.ForEach((it) => it.Dispose());
        _streamList.Clear();
    }
}
