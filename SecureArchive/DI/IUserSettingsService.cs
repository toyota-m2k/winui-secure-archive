using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

//internal enum SettingsKey {
//    DataFolder,
//    PortNo,
//    ServerAutoStart,
//    ServerName,
//    EnableHttps,
//    HttpsPort,
//    PfxPath,
//    PfxPasswordEncrypted,
//}

internal interface IReadonlyUserSettingsAccessor {
    string? DataFolder { get; }
    // int PortNo { get; }
    bool ServerAutoStart { get; }
    string? PreviousPeerHost { get; }
    bool ShowLog { get; }
    /// <summary>mDNS Service Instance 名 / ペアリング QR の表示名。空ならマシン名を使う。</summary>
    string? ServerName { get; }
    bool EnableMdnsAdvertisement { get; set; }

    /// <summary>EnsureServerName: ServerName が空ならマシン名を返す。</summary>
    string EnsureServerName { get; }
    bool EnableHttp { get; }
    bool EnableHttps { get; }
    int PortHttp { get; }
    int PortHttps { get; }
    bool ServerEnabled { get; }
    //bool HttpsOnly { get; }
    string? PfxPath { get; }
    /// <summary>DPAPI で暗号化された PFX パスワード (Base64)。生のパスワードは [PfxPassword] 経由で取得。</summary>
    string? PfxPasswordEncrypted { get; }
    /// <summary>PFX パスワード (DPAPI 復号後)。永続化時は [PfxPasswordEncrypted] が更新される。</summary>
    string PfxPassword { get; }
}
internal interface IUserSettingsAccessor : IReadonlyUserSettingsAccessor {
    new string? DataFolder { get; set; }
    //new int PortNo { get; set; }
    new bool ServerAutoStart { get; set; }
    new string? PreviousPeerHost { get; set; }
    new bool ShowLog { get; set; }
    new string? ServerName { get; set; }
    new bool EnableMdnsAdvertisement { get; set; }
    new bool EnableHttp { get; set; }
    new bool EnableHttps { get; set; }
    new int PortHttp { get; set; }
    new int PortHttps { get; set; }

    //new bool HttpsOnly { get; set; }
    new string? PfxPath { get; set; }
    new string? PfxPasswordEncrypted { get; set; }
    /// <summary>PFX パスワードを設定する。DPAPI で暗号化されて [PfxPasswordEncrypted] に保存される。</summary>
    new string PfxPassword { get; set; }
}

internal interface IUserSettingsService {
    //Task<string?> GetStringAsync(SettingsKey key);
    //Task<int> GetIntAsync(SettingsKey key, int defaultValue = 0);
    //Task<long> GetLongAsync(SettingsKey key, long defaultValue = 0);
    //Task PutAsync<T>(SettingsKey key, T value);
    Task EditAsync(Func<IUserSettingsAccessor, bool> editor);
    Task<IReadonlyUserSettingsAccessor> GetAsync();
}