using SecureArchive.Models.DB;

namespace SecureArchive.Models.CryptoStream;

internal interface ICryptoStreamContainer
{
    Stream Stream { get; }
    FileEntry FileEntry { get; }
}

internal interface ICryptoStreamHandler : IDisposable
{
    ICryptoStreamContainer LockStream(FileEntry fileEntry, long id);
    void UnlockStream(ICryptoStreamContainer container, long id);
}
