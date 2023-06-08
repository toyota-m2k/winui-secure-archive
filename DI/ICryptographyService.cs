using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal interface ICryptographyService {
    bool IsReady { get; }
    Task SetPasswordAsync(string password);
    Task ChangePasswordAsync(string password);
    Task EncryptStreamAsync(Stream inputStream, Stream outputStream, Action<long, long>? progress);
    Task DecryptStreamAsync(Stream inputStream, Stream outputStream, Action<long, long>? progress);
}
