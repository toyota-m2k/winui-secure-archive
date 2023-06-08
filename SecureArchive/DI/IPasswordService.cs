using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI {
    enum PasswordStatus {
        NotSet,
        NotChecked,
        Checked,
    }

    internal interface IPasswordService {
        Task<PasswordStatus> GetPasswordStatusAsync();
        Task<bool> CheckPasswordAsync(string password);
        Task<bool> SetPasswordAsync(string newPassword);
    }
}
