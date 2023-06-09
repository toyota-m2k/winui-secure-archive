﻿using System;
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
        Task<bool> CheckRemoteKey(string challenge, string? remoteKey);

        // Utility
        string CreateHashedPassword(string pwd);
        string CreatePassPhrase(string challenge, string pwd);
    }
}