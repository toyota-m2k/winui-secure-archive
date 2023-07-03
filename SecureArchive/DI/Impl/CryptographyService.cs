using Microsoft.Extensions.Logging;
using SecureArchive.Utils;
using SecureArchive.Utils.Crypto;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Windows.Security.Cryptography;

namespace SecureArchive.DI.Impl {
    internal class CryptographyService : ICryptographyService {
        const string KEY_CRYPT_KEY = "CryptKey";
        const string CRYPT_SEED = "VHUNU6Dz9h6K83bFmGcc36dGH2mRuA5aw0NomSefwxj2ALYDzq5Z8+cLYj9b";
        const string IV_SEED = "E6lTn1MxdwdvmF/FmCi28+LRbLKLphW95Fd+TwrSs4x9SJdQlc2h5u4y9gk0";

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
                _logger.Debug($"New CryptoKey: {newKey}");
                cryptedKey = cryptor.EncryptString(newKey).Base64String;
                await _localSettingsService.PutAsync(KEY_CRYPT_KEY, cryptedKey);
                _cryptoKey = newKey;
            }
            else {
                _logger.Debug("Restoring CryptoKey...");
                try {
                    var decryptedKey = cryptor.DecryptString(BinaryData.FromBase64String(cryptedKey));
                    _cryptoKey = decryptedKey;
                    _logger.Debug($"CryptoKey Restored: {decryptedKey}");
                }
                catch (Exception ex) {
                    _logger.Error(ex, "CryptoKey Not Restored.");
                    _cryptoKey = null;
                    throw;
                }
            }
        }

        public async Task ChangePasswordAsync(string password) {
            if (_cryptoKey == null) {
                _logger.Error("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }

            var cryptor = SymmetricCrypt.Create(password, CRYPT_SEED, IV_SEED);
            var cryptedKey = cryptor.EncryptString(_cryptoKey).Base64String;
            await _localSettingsService.PutAsync(KEY_CRYPT_KEY, cryptedKey);
        }

        //public bool OpenStreamForEncryption(Stream outputStream, Func<Stream, bool> writer) {
        //    if (_cryptoKey == null) {
        //        _logger.LogError("No CryptoKey ... SetPassword in advance.");
        //        throw new InvalidOperationException("call SetPassword to set old password earlier.");
        //    }
        //    var aes = Aes.Create();
        //    //aes.Key = HashHelper.SHA256(_cryptoKey, CRYPT_SEED).Hash;
        //    aes.Key = Guid.Parse(_cryptoKey).ToByteArray();     // 128ビット ... AES の最短鍵長 ... これで十分。
        //    aes.IV = HashHelper.MD5(_cryptoKey, IV_SEED).Hash;  // 16バイト(128ビット）
        //    using (CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
        //        if (!writer(cryptoStream)) {
        //            return false;
        //        }
        //        cryptoStream.FlushFinalBlock();
        //        cryptoStream.Flush();
        //        return true;
        //    }
        //}



        public CryptoStream OpenStreamForEncryption(Stream outputStream) { 
            if (_cryptoKey == null) {
                _logger.Error("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }
            var aes = Aes.Create();
            //aes.Key = HashHelper.SHA256(_cryptoKey, CRYPT_SEED).Hash;
            aes.Key = Guid.Parse(_cryptoKey).ToByteArray();     // 128ビット ... AES の最短鍵長 ... これで十分。
            aes.IV = HashHelper.MD5(_cryptoKey, IV_SEED).Hash;  // 16バイト(128ビット）
            return new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        }


        public async Task EncryptStreamAsync(Stream inputStream, Stream outputStream, ProgressProc? progress) {
            if (_cryptoKey == null) {
                _logger.Error("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }
            await Task.Run(() => {
                using (CryptoStream cryptoStream = OpenStreamForEncryption(outputStream)) {
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

        public Stream OpenStreamForDecryption(Stream inputStream) {
            if (_cryptoKey == null) {
                _logger.Error("No CryptoKey ... SetPassword in advance.");
                throw new InvalidOperationException("call SetPassword to set old password earlier.");
            }
            var aes = Aes.Create();
            //aes.Key = HashHelper.SHA256(_cryptoKey, CRYPT_SEED).Hash;
            aes.Key = Guid.Parse(_cryptoKey).ToByteArray();     // 128ビット ... AES の最短鍵長 ... これで十分。
            aes.IV = HashHelper.MD5(_cryptoKey, IV_SEED).Hash;  // 16バイト(128ビット）
            return new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        }

        public async Task DecryptStreamAsync(Stream inputStream, Stream outputStream, ProgressProc? progress) {
            await Task.Run(() => {
                using (var cryptoStream = OpenStreamForDecryption(inputStream)) { 
                    var buffer = new byte[4096];
                    int len;
                    long total = inputStream.Length;
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
