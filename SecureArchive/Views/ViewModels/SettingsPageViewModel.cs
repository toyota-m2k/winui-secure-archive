using io.github.toyota32k.toolkit.net;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.DI.Impl;
using SecureArchive.Utils;
using System.Diagnostics;
using System.Reactive.Linq;

namespace SecureArchive.Views.ViewModels {
    internal class SettingsPageViewModel {
        private IAppConfigService _appConfigService;
        private IUserSettingsService _userSettingsService;
        private IPasswordService _passwordService;
        private IFileStoreService _fileStoreService;
        private IPageService _pageService;
        private IHttpServreService _httpServreService;
        private ISecureStorageService _secureStorageService;
        private IBackupService _backupService;
        private IDatabaseService _databaseService;

        //public ReactivePropertySlim<bool> DataFolderRegistered = new(false);
        public ReactivePropertySlim<string> DataFolder { get; } = new ("");
        public ReactivePropertySlim<int> PortNo { get; } = new(0);
        public ReactivePropertySlim<bool> ServerAutoStart { get; } = new(false);
        //private ReactivePropertySlim<PasswordStatus?> CurrentPasswordStatus { get; } = new(null);

        //public ReadOnlyReactivePropertySlim<bool> Initialized { get; }
        //public ReadOnlyReactivePropertySlim<bool> NeedToSetPassword { get; }
        //public ReadOnlyReactivePropertySlim<bool> NeedToCheckPassword { get; }
        //public ReadOnlyReactivePropertySlim<bool> PasswordChecked { get; }
        //public ReadOnlyReactivePropertySlim<bool> NeedToSetDataFolder { get; }
        //public ReadOnlyReactivePropertySlim<bool> AllReady { get; }

        public ReactivePropertySlim<string> PasswordString { get; } = new("");
        public ReactivePropertySlim<string> PasswordConfirmString { get; } = new("");
        public ReactivePropertySlim<bool> ChangingPassword { get; } = new(false);

        public ReadOnlyReactivePropertySlim<bool> PasswordReady { get; }

        public ReactiveCommandSlim PasswordCommand { get; } = new();
        public ReactiveCommandSlim ChangePasswordCommand { get; } = new();
        public ReactiveCommandSlim CancelPasswordCommand { get; } = new();
        public ReactiveCommandSlim SelectFolderCommand { get; } = new();
        public ReactiveCommandSlim DoneCommand { get; } = new();
        public ReactiveCommandSlim ExportCommand { get; } = new();
        public ReactiveCommandSlim ImportCommand { get; } = new();

        private bool CheckPasswordRequired => _pageService.CheckPasswordRequired;

        public enum Status {
            Initializing,
            NeedToSetPassword,
            NeedToCheckPassword,
            NeedToSetDataFolder,
            Ready,
        }
        public ReactivePropertySlim<Status> PanelStatus = new (Status.Initializing);

        private async Task AdvanceStatus() {
            switch (PanelStatus.Value) {
                case Status.Initializing:
                    if(await _passwordService.GetPasswordStatusAsync()==PasswordStatus.NotSet) {
                        PanelStatus.Value = Status.NeedToSetPassword;
                    } else {
                        PanelStatus.Value = Status.NeedToCheckPassword;
                    }
                    break;
                case Status.NeedToSetPassword:
                case Status.NeedToCheckPassword:
                    if (await _passwordService.GetPasswordStatusAsync() != PasswordStatus.Checked) {
                        return;
                    }
                    if (await _fileStoreService.IsReady()) {
                        if (CheckPasswordRequired) {
                            Done();
                            return;
                        }
                        PanelStatus.Value = Status.Ready;
                    }
                    else {
                        PanelStatus.Value = Status.NeedToSetDataFolder;
                    }
                    break;
                case Status.NeedToSetDataFolder:
                    if(!await _fileStoreService.IsReady()) {
                        return;
                    }
                    PanelStatus.Value = Status.Ready;
                    break;
                case Status.Ready:
                    break;
            }
        }

        public SettingsPageViewModel(
            IAppConfigService appConfigService,
            IUserSettingsService userSettingsService, 
            IPasswordService passwordService,
            IFileStoreService fileStoreSercice,
            ISecureStorageService secureStorageService,
            IPageService pageService,
            IHttpServreService httpServreService,
            IFileStoreService fileStoreService,
            IBackupService backupService,
            IDatabaseService databaseService
            ) {
            _appConfigService = appConfigService;
            _userSettingsService = userSettingsService;
            _passwordService = passwordService;
            _fileStoreService = fileStoreSercice;
            _pageService = pageService;
            _httpServreService = httpServreService;
            _secureStorageService = secureStorageService;
            _backupService = backupService;
            _databaseService = databaseService;

            //Initialized = CurrentPasswordStatus.Select((it) => it != null).ToReadOnlyReactivePropertySlim<bool>();
            //NeedToSetPassword = CurrentPasswordStatus.Select((it)=> it==PasswordStatus.NotSet).ToReadOnlyReactivePropertySlim<bool>();
            //NeedToCheckPassword = CurrentPasswordStatus.Select((it)=> it==PasswordStatus.NotChecked).ToReadOnlyReactivePropertySlim<bool>();
            //PasswordChecked = CurrentPasswordStatus.Select((it)=> it==PasswordStatus.Checked).ToReadOnlyReactivePropertySlim<bool>();
            //NeedToSetDataFolder = DataFolderRegistered.CombineLatest(PasswordChecked, (folder, pwd) => !folder && pwd).ToReadOnlyReactivePropertySlim();
            //AllReady = PasswordChecked.CombineLatest(NeedToSetDataFolder, (pwd,folder)=> pwd && !folder).ToReadOnlyReactivePropertySlim();
            PasswordReady = PasswordString.CombineLatest(PasswordConfirmString, PanelStatus, (pwd, cpwd, status) => {
                if(status == Status.NeedToCheckPassword) {
                    return !string.IsNullOrEmpty(pwd);
                } else if(status == Status.NeedToSetPassword) {
                    return !string.IsNullOrEmpty(pwd) && pwd == cpwd;
                } else {
                    return false;
                }
            }).ToReadOnlyReactivePropertySlim<bool>();
            PasswordCommand.Subscribe(HandlePassword);
            SelectFolderCommand.Subscribe(SelectFolder);
            DoneCommand.Subscribe(Done);
            ChangePasswordCommand.Subscribe(ChangePassword);
            CancelPasswordCommand.Subscribe(CancelPassword);
            ExportCommand.Subscribe(ExportSettings);
            ImportCommand.Subscribe(ImportSettings);
            Initialize();
        }

        private void CancelPassword() {
            if (ChangingPassword.Value) {
                PanelStatus.Value = Status.Ready;
                ChangingPassword.Value = false;
            }
        }

        private void ChangePassword() {
            PasswordString.Value = "";
            PasswordConfirmString.Value = "";
            ChangingPassword.Value = true;
            PanelStatus.Value = Status.NeedToSetPassword;
        }

        private void ExportSettings() {
            _ = _backupService.BackupLocalDB();
        }

        private void SafeDeleteFile(string filePath) {
            if (File.Exists(filePath)) {
                try {
                    File.Delete(filePath);
                }
                catch (Exception e) {
                    Debug.WriteLine($"Failed to delete file {filePath}: {e.Message}");
                }
            }
        }

        private async void ImportSettings() {
            var dbPath = _appConfigService.DBPath;
            var settingsPath = _appConfigService.SettingsPath;
            if (File.Exists(dbPath) || File.Exists(settingsPath)) {
                var decision = await MessageBoxBuilder.Create(App.MainWindow)
                    .SetMessage("The current database or settings file already exists.\r\nDo you want to overwrite it?")
                    .AddButton("OK", true)
                    .AddButton("Cancel", false)
                    .ShowAsync();
                if (decision as bool? != true) {
                    return;
                }
            }
            var dstFolder = await FolderPickerBuilder.Create(App.MainWindow)
                .SetIdentifier("SA.BackupDB")
                .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                .PickAsync();
            if (dstFolder == null) {
                // cancelled
                return;
            }
            var srcDbFile = Path.Combine(dstFolder.Path, _appConfigService.DBName);
            var srcSettingsFile = Path.Combine(dstFolder.Path, _appConfigService.SettingsName);
            if (!File.Exists(srcDbFile) || !File.Exists(srcSettingsFile)) {
                await MessageBoxBuilder.Create(App.MainWindow)
                    .SetMessage("The selected folder does not contain the required files.")
                    .AddButton("OK")
                    .ShowAsync();
                return;
            }
            try {
                _databaseService.Dispose();
                File.Copy(srcSettingsFile, settingsPath, true);
                File.Copy(srcDbFile, dbPath, true);
                await MessageBoxBuilder.Create(App.MainWindow)
                    .SetMessage("All settings and databases have been imported.")
                    .AddButton("Restart Now")
                    .ShowAsync();
            }
            catch (Exception e) {
                await MessageBoxBuilder.Create(App.MainWindow)
                    .SetMessage($"Failed to import settings: {e.Message}")
                    .AddButton("Restart Now")
                    .ShowAsync();
                SafeDeleteFile(dbPath);
                SafeDeleteFile(settingsPath);
            }

            _appConfigService.Restart();
        }

        private async void Done() {
            await _userSettingsService.EditAsync((editor) => {
                var changed = false;
                if (editor.PortNo != PortNo.Value) {
                    editor.PortNo = PortNo.Value;
                    changed = true;
                }
                if(editor.ServerAutoStart != ServerAutoStart.Value) { 
                    editor.ServerAutoStart = ServerAutoStart.Value;
                    changed = true;
                }
                return changed;
            });
            _appConfigService.NeedsConfirmOnExit = true;
            _pageService.ShowMenuPage();
        }

        private async void SelectFolder() {
            var folder = await FolderPickerBuilder.Create(App.MainWindow)
                .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                .SetIdentifier("AS.DataFolder")
                .PickAsync();
            if(folder!=null) {
                if(!await folder.IsEmpty()) {
                    var decision = await MessageBoxBuilder.Create(App.MainWindow)
                        .SetMessage("The specified folder is not empty. \r\nAre you sure you want to set it as the data folder?")
                        .AddButton("OK", true)
                        .AddButton("Cancel", false)
                        .ShowAsync();
                    if(decision as bool? != true) {
                        return;
                    }
                }

                var oldPath = await _fileStoreService.GetFolder();
                if (oldPath != null && Directory.Exists(oldPath) && !FileUtils.IsFolderEmpty(oldPath)) {
                    var result = await MessageBoxBuilder.Create(App.MainWindow)
                        .SetMessage("Are you sure to move the data folder?")
                        .AddButton("OK", null, true)
                        .AddButton("Cancel", null, false)
                        .ShowAsync();
                    if((bool?)result != true) return;

                }

                if (await _secureStorageService.SetStorageFolder(folder.Path)) {
                    DataFolder.Value = folder.Path;
                }
            }
            //DataFolderRegistered.Value = _fileStoreSercice.IsRegistered;
            await AdvanceStatus();
        }

        private bool reentrantCheck = false;
        private async void HandlePassword() {
            if (reentrantCheck) return;
            reentrantCheck = true;
            try {
                switch (PanelStatus.Value) {
                    case Status.NeedToSetPassword:
                        if (await _passwordService.SetPasswordAsync(PasswordString.Value)) {
                            await AdvanceStatus();
                        }
                        break;
                    case Status.NeedToCheckPassword:
                        if (await _passwordService.CheckPasswordAsync(PasswordString.Value)) {
                            await AdvanceStatus();
                        }
                        break;
                    default:
                        break;
                }
            } finally {
                reentrantCheck = false;
                ChangingPassword.Value = false;
                PasswordString.Value = "";
                PasswordConfirmString.Value = "";
            }
        }

        private async void Initialize() {
            _httpServreService.Stop();
            await _userSettingsService.EditAsync((editor) => {
                //DataFolder.Value = editor.DataFolder ?? "";
                PortNo.Value = editor.PortNo;
                ServerAutoStart.Value = editor.ServerAutoStart;
                return false;
            });
            DataFolder.Value = (await _fileStoreService.GetFolder()) ?? "";
            await AdvanceStatus();
        }


    }
}
