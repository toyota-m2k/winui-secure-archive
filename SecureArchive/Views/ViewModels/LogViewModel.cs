using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace SecureArchive.Views.ViewModels;

public class LogMessage {
    public UtLog.Level Level { get; }
    public string Message { get; }
    public LogMessage(UtLog.Level level, string message) {
        Level = level;
        Message = message;
    }
}


internal class LogViewModel {
    private const int STATUS_MAX_LINES = 500; // �ő�s��
    private readonly ObservableCollection<LogMessage> _logMessages = new();
    public ReadOnlyObservableCollection<LogMessage> LogMessages { get; }
    private readonly IMainThreadService _mainThreadService;
    public ReactivePropertySlim<bool> StopScroll = new(false);
    public ReactiveCommandSlim ClearCommand = new();
    public ReactiveCommandSlim CopyCommand = new();

    public LogViewModel(IMainThreadService mainThreadService) {
        LogMessages = new ReadOnlyObservableCollection<LogMessage>(_logMessages);
        _mainThreadService = mainThreadService;
        ClearCommand.Subscribe(() => {
            _logMessages.Clear();
        });

        CopyCommand.Subscribe(() => {
            // _logMessages �����s��؂�̕�����ɕϊ�
            var sb = _logMessages.Aggregate(new StringBuilder(), (acc, log) => {
                acc.AppendLine($"[{log.Level}] {log.Message}");
                return acc;
            });
            // �N���b�v�{�[�h�ɃR�s�[
            var dataPackage = new DataPackage();
            dataPackage.SetText(sb.ToString());
            Clipboard.SetContent(dataPackage);
        });
    }

    public void AddLog(UtLog.Level level, string message) {
        _mainThreadService.Run(() => {
            // ���O��ǉ�
            _logMessages.Add(new LogMessage(level, message));

            // �s���� STATUS_MAX_LINES �𒴂����ꍇ�A�Â����O���폜
            while (_logMessages.Count > STATUS_MAX_LINES) {
                _logMessages.RemoveAt(0);
            }
        });
    }

    public void ClearLog() {
        _logMessages.Clear();
    }
}
