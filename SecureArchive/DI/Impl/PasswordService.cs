using Microsoft.Extensions.Logging;
using SecureArchive.Utils;
using SecureArchive.Utils.Crypto;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Windows.Security.Cryptography;

namespace SecureArchive.DI.Impl {
    internal class PasswordService: IPasswordService {
        const string KEY_PASSWORD = "Password";
        const string PWD_SEED = "y6c46S/PBqd1zGFwghK2AFqvSDbdjl+YL/DKXgn/pkECj0x2fic5hxntizw5";
        PasswordStatus _passwordStatus = PasswordStatus.NotChecked;
        ILocalSettingsService _localSettingsService;
        ICryptographyService _cryptographyService;
        ILogger _logger;

        bool _isInitialized = false;
        string? _hashedPassword = null;

        
        public PasswordService(ILocalSettingsService localSettingsService, ICryptographyService cryptographyService, ILoggerFactory loggerFactory) {
            _localSettingsService = localSettingsService;
            _cryptographyService = cryptographyService;
            _logger = loggerFactory.CreateLogger("Pwd");
        }

        private async Task Initialize() {
            if(!_isInitialized) {
                _isInitialized = true;
                _hashedPassword = await _localSettingsService.GetAsync<string>(KEY_PASSWORD);
                if(string.IsNullOrEmpty(_hashedPassword)) { 
                    _passwordStatus = PasswordStatus.NotSet;
                }
                //for (var i = 0; i < 10; i++) {
                //    var rand = RandomNumberGenerator.GetBytes(45);
                //    var sr = CryptographicBuffer.EncodeToBase64String(rand.AsBuffer());
                //    _logger.Debug(sr);
                //}

                _logger.Debug($"Initialized: state={_passwordStatus}");
            }
        }

        public async Task<bool> SetPasswordAsync(string newPassword) {
            await Initialize();
            if(string.IsNullOrEmpty(newPassword)) {
                _logger.Error($"password must not be empty.");
                return false;
            }
            if(_passwordStatus == PasswordStatus.NotChecked) {
                _logger.Error($"check password on ahead.");
                return false;
            }
            try {
                if (_passwordStatus == PasswordStatus.NotSet) {
                    await _cryptographyService.SetPasswordAsync(newPassword);
                } else {
                    await _cryptographyService.ChangePasswordAsync(newPassword);
                }
            } catch (Exception ex) {
                _logger.Error(ex, "change password error in CryptograpyService.");
                return false;
            }
            _hashedPassword = CreateHashedPassword(newPassword); // HashHelper.SHA256(newPassword, PWD_SEED).AsHexString;
            await _localSettingsService.PutAsync(KEY_PASSWORD, _hashedPassword);
            _passwordStatus = PasswordStatus.Checked;
            return true;
        }

        public async Task<bool> CheckPasswordAsync(string password) {
            await Initialize();
            if(string.IsNullOrEmpty(_hashedPassword)) {
                _logger.Error("given password is empty.");
                return false;
            }
            if(_hashedPassword != HashHelper.SHA256(password, PWD_SEED).AsHexString) {
                _logger.Error("given password is not match.");
                return false;
            }
            try {
                await _cryptographyService.SetPasswordAsync(password);
                _logger.Debug("password checked.");
                _passwordStatus = PasswordStatus.Checked;
                return true;
            } catch (Exception ex) {
                _logger.Error(ex, "cannot set password to crypt service.");
                return false;
            }
        }

        public async Task<PasswordStatus> GetPasswordStatusAsync() {
            await Initialize();
            return _passwordStatus;
        }

        public async Task<bool> CheckRemoteKey(string challenge, string? remoteKey) {
            await Initialize();
            if (string.IsNullOrEmpty(_hashedPassword)) {
                _logger.Error("no password has been set yet.");
                throw new InvalidOperationException("no password has been set yet.");
            }
            if (remoteKey == null) return false;
            return CreatePassPhrase(challenge, _hashedPassword) == remoteKey;
        }

        public string CreateHashedPassword(string pwd) {
            return HashHelper.SHA256(pwd, PWD_SEED).AsHexString;
        }
        public string CreatePassPhrase(string challenge, string pwd) {
            return HashHelper.SHA256(challenge, pwd).AsBase64String;
        }


    }
}
