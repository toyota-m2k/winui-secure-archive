using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI {
    /**
     * アプリの動作に関する設定や定数などの情報を発信するサービス
     */
    public interface IAppConfigService {
        /**
         * MainWindowのタイトルバーに表示する名前
         */
        string AppTitle { get; }
        /**
         * MainWindowのタイトルバーに表示するアイコン （hIconのパス）
         */
        string AppIconPath { get; }
        /**
         * アプリ名（データフォルダの名前になる）
         */
        string AppName { get; }
        /**
         * アプリバージョン
         */
        Version AppVersion { get; }
        string AppDataPath { get; }
        string DBName{ get; }
        string DBPath { get; }
        string SettingsName { get; }
        string SettingsPath { get; }
        bool IsMSIX { get; }

        bool NeedsConfirmOnExit { get; set; }
        void Restart();
    }
}
