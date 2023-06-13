using System.Security.Cryptography;

namespace SecureArchive.DI;

internal interface ICryptographyService {
    bool IsReady { get; }
    Task SetPasswordAsync(string password);
    Task ChangePasswordAsync(string password);
    Task EncryptStreamAsync(Stream inputStream, Stream outputStream, ProgressProc? progress);
    Task DecryptStreamAsync(Stream inputStream, Stream outputStream, ProgressProc? progress);
    //bool OpenStreamForEncryption(Stream outputStream, Func<Stream,bool> writer);
    CryptoStream OpenStreamForEncryption(Stream outputStream);
    Stream OpenStreamForDecryption(Stream inputStream);
}
