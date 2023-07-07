using Reactive.Bindings;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Views.ViewModels; 

public class RemotePasswordDialogViewModel {
    public ReactivePropertySlim<string> Password { get; } = new("");
    public ReadOnlyReactivePropertySlim<bool> IsReady { get; }
    public RemotePasswordDialogViewModel() {
        IsReady = Password.Select(x => x.IsNotEmpty()).ToReadOnlyReactivePropertySlim();
    }
}
