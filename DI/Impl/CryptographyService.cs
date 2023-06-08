using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.Utils.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace SecureArchive.DI.Impl
{
    internal class CryptographyService : ICryptographyService {
        const string KEY_CRYPT_KEY = "CryptKey";
        const string CRYPT_SEED = "WTiefd$d$a&W2f39=RE%HGfe:ee7g";
        const string IV_SEED = "Ef89jI@qg8g1ssi0=3ua*uU&iIO19)42";

        ILocalSettingsService _localSettingsService;
        ILogger _logger;

        public CryptographyService(ILocalSettingsService localSettingsService, ILoggerFactory loggerFactory) {
            _localSettingsService = localSettingsService;
            _logger = loggerFactory.CreateLogger("Crypto");
        }

        string? _cryptoKey = null;

        public bool IsReady => _cryptoKey != null;

        public async Task SetPasswordAsync(string password) {
            var cryptor = SymmetricCrypt.Create(password, CRYPT_SEED, IV_SEED);
            var cryptedKey = await _localSettingsService.GetAsync<string>(KEY_CRYPT_KEY);
            if (string.IsNullOrEmpty(cryptedKey)) {
                var newKey = Guid.NewGuid().ToString();
                _logger.LogDebug($"New CryptoKey: {newKey}");
                cryptedKey = cryptor.EncryptString(newKey).Base64String;
                await _localSettingsService.PutAsync(KEY_CRYPT_KEY, cryptedKey);
                _cryptoKey = newKey;
            }
            else {
                _logger.LogDebug("Restoring CryptoKey...");
                try {
                    var decryptedKey = cryptor.DecryptString(BinaryData.FromBase64String(cryptedKey));
                    _cryptoKey = decryptedKey;
                    _logger.LogDebug($"CryptoKey Restored: {decryptedKey}");
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "CryptoKey Not Restored.");
                    _cryptoKey = null;
                    throw;
                }
            }
        }

        public async Task ChangePasswordAsync(string password) {
            if (_cryptoKey == null) {
                _logger.LogError("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }

            var cryptor = SymmetricCrypt.Create(password, CRYPT_SEED, IV_SEED);
            var cryptedKey = cryptor.EncryptString(_cryptoKey).Base64String;
            await _localSettingsService.PutAsync(KEY_CRYPT_KEY, cryptedKey);
        }

        public async Task EncryptStreamAsync(Stream inputStream, Stream outputStream, Action<long, long>? progress) {
            if (_cryptoKey == null) {
                _logger.LogError("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }
            await Task.Run(() => {
                var aes = Aes.Create();
                aes.Key = Guid.Parse(_cryptoKey).ToByteArray();
                using (CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                    var buffer = new byte[4096];
                    int len;
                    long total = inputStream.Length;
                    long current = 0;
                    while (true) {
                        len = inputStream.Read(buffer, 0, buffer.Length);
                        if (len <= 0) break;
                        cryptoStream.Write(buffer, 0, len);
                        current += len;
                        progress?.Invoke(current, total);
                    }
                    cryptoStream.FlushFinalBlock();
                }
            });
        }

        public async Task DecryptStreamAsync(Stream inputStream, Stream outputStream, Action<long, long>? progress) {
            if (_cryptoKey == null) {
                _logger.LogError("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }
            await Task.Run(() => {
                var aes = Aes.Create();
                aes.Key = Guid.Parse(_cryptoKey).ToByteArray();
                using (CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                    var buffer = new byte[4096];
                    int len;
                    long total = cryptoStream.Length;
                    long current = 0;
                    while (true) {
                        len = cryptoStream.Read(buffer, 0, buffer.Length);
                        if (len <= 0) break;
                        outputStream.Write(buffer, 0, len);
                        current += len;
                        progress?.Invoke(current, total);
                    }
                    //cryptoStream.FlushFinalBlock();
                    outputStream.Flush();
                }
            });
        }
    }
}
