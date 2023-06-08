using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Crypto;

namespace SecureArchive.DI.Impl {
    internal class PasswordService: IPasswordService {
        const string KEY_PASSWORD = "Password";
        const string PWD_SEED = "gewq#idsE%Dfa&ewqjo8S(33s2f66$";
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
                _hashedPassword = await _localSettingsService.GetAsync<string>(KEY_PASSWORD);
                if(string.IsNullOrEmpty(_hashedPassword)) { 
                    _passwordStatus = PasswordStatus.NotSet;
                }
                _logger.LogDebug($"Initialized: state={_passwordStatus}");
            }
        }


        public async Task<bool> SetPasswordAsync(string newPassword) {
            await Initialize();
            if(string.IsNullOrEmpty(newPassword)) {
                _logger.LogError($"password must not be empty.");
                return false;
            }
            if(_passwordStatus == PasswordStatus.NotChecked) {
                _logger.LogError($"check password on ahead.");
                return false;
            }
            try {
                if (_passwordStatus == PasswordStatus.NotSet) {
                    await _cryptographyService.SetPasswordAsync(newPassword);
                } else {
                    await _cryptographyService.ChangePasswordAsync(newPassword);
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "change password error in CryptograpyService.");
                return false;
            }
            await _localSettingsService.PutAsync(KEY_PASSWORD, HashHelper.SHA256(newPassword, PWD_SEED).AsHexString);
            _passwordStatus = PasswordStatus.Checked;
            return true;
        }

        public async Task<bool> CheckPasswordAsync(string password) {
            await Initialize();
            if(string.IsNullOrEmpty(_hashedPassword)) {
                _logger.LogError("given password is empty.");
                return false;
            }
            if(_hashedPassword != HashHelper.SHA256(password, PWD_SEED).AsHexString) {
                _logger.LogError("given password is not match.");
                return false;
            }
            try {
                await _cryptographyService.SetPasswordAsync(password);
                _logger.LogDebug("password checked.");
                _passwordStatus = PasswordStatus.Checked;
                return true;
            } catch (Exception ex) {
                _logger.LogError(ex, "cannot set password to crypt service.");
                return false;
            }
        }

        public async Task<PasswordStatus> GetPasswordStatusAsync() {
            await Initialize();
            return _passwordStatus;
        }
    }
}
