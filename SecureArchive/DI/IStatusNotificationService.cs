using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

public delegate void UpdateMessageProc(string newMessage);
public delegate void ProgressProc(long current, long total);
public delegate Task WithProgressProc(UpdateMessageProc updateMessage, ProgressProc progress);
public delegate Task WithBusyProc(UpdateMessageProc updateMessage);

internal enum ProgressMode {
    None,
    Information,
    WaitRing,
    ProgressBar,
}

internal interface IStatusNotificationService {
    IReadOnlyReactiveProperty<string> Message { get; }
    IReadOnlyReactiveProperty<int> ProgressInPercent { get; }
    IReadOnlyReactiveProperty<ProgressMode> ProgressMode { get; }
    Task WithProgress(string initialMessage, WithProgressProc proc);
    Task WithBusy(string initialMessage, WithBusyProc proc);
    void ShowMessage(string message, int termInMs);
}
