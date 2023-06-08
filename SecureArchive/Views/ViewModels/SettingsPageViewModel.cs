using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils;
using System.Reactive.Linq;
using Windows.UI.Popups;

namespace SecureArchive.Views.ViewModels {
    internal class SettingsPageViewModel {
        private IUserSettingsService _userSettingsService;
        private IPasswordService _passwordService;
        private IFileStoreService _fileStoreSercice;
        private IPageService _pageService;

        //public ReactivePropertySlim<bool> DataFolderRegistered = new(false);
        public ReactivePropertySlim<string> DataFolder { get; } = new ("");
        public ReactivePropertySlim<int> PortNo { get; } = new(0);
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
                    if (await _fileStoreSercice.IsRegistered()) {
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
                    if(!await _fileStoreSercice.IsRegistered()) {
                        return;
                    }
                    PanelStatus.Value = Status.Ready;
                    break;
                case Status.Ready:
                    break;
            }
        }

        public SettingsPageViewModel(
            IUserSettingsService userSettingsService, 
            IPasswordService passwordService,
            IFileStoreService fileStoreSercice,
            IPageService pageService
            ) {
            _userSettingsService = userSettingsService;
            _passwordService = passwordService;
            _fileStoreSercice = fileStoreSercice;
            _pageService = pageService;
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

        private async void Done() {
            await _userSettingsService.EditAsync((editor) => {
                if(editor.PortNo != PortNo.Value) {
                    editor.PortNo = PortNo.Value;
                    return true;
                } else {
                    return false;
                }
            });
            _pageService.ShowMenuPage();
        }

        private async void SelectFolder() {
            var folder = await FolderPickerBuilder.Create(App.MainWindow)
                .SetViewMode(Windows.Storage.Pickers.PickerViewMode.List)
                .SetIdentifier("AS.DataFolder")
                .PickAsync();
            if(folder!=null) {
                if(!await folder.IsEmpty()) {
                    await MessageBoxBuilder.Create(App.MainWindow)
                        .SetMessage("This folder contains one or more files. Please select empty folder.")
                        .AddButton("OK")
                        .ShowAsync();
                    return;
                }

                if (await _fileStoreSercice.Register(folder.Path)) {
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
            await _userSettingsService.EditAsync((editor) => {
                //DataFolder.Value = editor.DataFolder ?? "";
                PortNo.Value = editor.PortNo;
                return false;
            });
            DataFolder.Value = (await _fileStoreSercice.GetFolder()) ?? "";
            await AdvanceStatus();
        }


    }
}
