using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI {
    /**
     * ユーザーによる設定を保存/取得するサービス
     */
    public interface ILocalSettingsService {
        Task<T?> GetAsync<T>(string key);

        Task PutAsync<T>(string key, T value);
    }
}
