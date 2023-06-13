using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using SecureArchive.Utils;

namespace SecureArchive.DI.Impl;
internal class StatusNotificationService : IStatusNotificationService {
    IMainThreadService _mainThreadService;
    ILogger _logger;
    public StatusNotificationService(IMainThreadService mainThreadService, ILoggerFactory loggerFactory) { 
        _mainThreadService = mainThreadService;
        _logger = loggerFactory.CreateLogger<StatusNotificationService>();
    }

    private AtomicInteger _idGenerator = new();
    public ReactivePropertySlim<string> Message { get; } = new("");
    public ReactivePropertySlim<int> ProgressInPercent { get; } = new(-1);
    public ReactivePropertySlim<ProgressMode> ProgressMode { get; } = new(DI.ProgressMode.None);

    IReadOnlyReactiveProperty<string> IStatusNotificationService.Message => Message;

    IReadOnlyReactiveProperty<int> IStatusNotificationService.ProgressInPercent => ProgressInPercent;

    IReadOnlyReactiveProperty<ProgressMode> IStatusNotificationService.ProgressMode => ProgressMode;

    private int SetMessage(string message, ProgressMode mode) {
        return _mainThreadService.Run(() => {
            ProgressInPercent.Value = 0;
            Message.Value = message;
            ProgressMode.Value = mode;
            return _idGenerator.IncrementAndGet();
        });
    }
    private int UpdateMessage(string message) {
        return _mainThreadService.Run(() => {
            Message.Value = message;
            return _idGenerator.IncrementAndGet();
        });
    }
    private void ResetMessage(int id) {
        _mainThreadService.Run(() => {
            if (_idGenerator.Get() == id) {
                Message.Value = "";
            }
            ProgressMode.Value = DI.ProgressMode.None;
            ProgressInPercent.Value = 0;
        });
    }
    private void SetProgress(long current, long total) {
        _mainThreadService.Run(() => {
            if (total == 0) {
                ProgressInPercent.Value = 0;
            } else if(current>total) {
                ProgressInPercent.Value = 100;
            }
            else {
                ProgressInPercent.Value = (int)((current * 100) / total);
            }
        });
    }

    public async Task WithProgress(string initialMessage, WithProgressProc proc) {
        int id = SetMessage(initialMessage, DI.ProgressMode.ProgressBar);
        try {
            await proc(
                (message) => {
                    id = UpdateMessage(message);
                }, SetProgress);
        } finally {
            await Task.Delay(2000);
            ResetMessage(id);
        }
    }

    public async Task WithBusy(string initialMessage, WithBusyProc proc) {
        int id = SetMessage(initialMessage, DI.ProgressMode.WaitRing);
        try {
            await proc(
                (message) => {
                    id = UpdateMessage(message);
                });
        }
        finally {
            await Task.Delay(2000);
            ResetMessage(id);
        }
    }

    public async void ShowMessage(string message, int termInMs) {
        int id = SetMessage(message, DI.ProgressMode.Information);
        await Task.Delay(termInMs);
        ResetMessage(id);
    }
}
