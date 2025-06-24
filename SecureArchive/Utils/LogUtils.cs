using Microsoft.Extensions.Logging;
using SecureArchive.Views.ViewModels;
using System.Runtime.CompilerServices;

namespace SecureArchive.Utils;

public class UtLog {
    static private ILogger _globalLogger = null!;
    static private LogViewModel _logViewModel = null!;

    public enum Level {
        Debug, Info, Warn, Error, Fatal
    }

    static class LogWrapper {
        public static void Log(Level level, string message) {
            if (_globalLogger != null) {
                switch (level) {
                    case Level.Debug:
                        _globalLogger.LogDebug(message);
                        break;
                    case Level.Info:
                        _globalLogger.LogInformation(message);
                        break;
                    case Level.Warn:
                        _globalLogger.LogWarning(message);
                        break;
                    case Level.Error:
                        _globalLogger.LogError(message);
                        break;
                    case Level.Fatal:
                        _globalLogger.LogCritical(message);
                        break;
                }
            } else {
                // ログサービスがセットされていない場合は、Debug出力する
                System.Diagnostics.Debug.WriteLine(message);
            }
            _logViewModel?.AddLog(level, message);
        }
        public static void LogError(Exception exception, string message) {
            if (_globalLogger != null) {
                _globalLogger.LogError(exception, message);
            }
            else {
                // ログサービスがセットされていない場合は、Debug出力する
                System.Diagnostics.Debug.WriteLine(message);
                System.Diagnostics.Debug.WriteLine(exception.Message);
                if (exception.StackTrace != null) {
                    System.Diagnostics.Debug.WriteLine(exception.StackTrace);
                }
            }
            _logViewModel?.AddLog(Level.Error, message);
            _logViewModel?.AddLog(Level.Error, exception.Message);
            if (exception.StackTrace != null) {
                _logViewModel?.AddLog(Level.Error, exception.StackTrace);
            }
        }
    }

    /**
     * Loggerサービスを参照しないモジュールからもログ出力できるよう、Appの初期化時に、globalLoggerをセットしておく。
     */
    internal static void SetGlobalLogger(ILogger logger, LogViewModel logViewModel) {
        _globalLogger = logger;
        _logViewModel = logViewModel;
    }
    /**
     * new UtLog("prefix").Debug("message"); と書くのがなんか気持ち悪い（個人の感想です）ので、
     * UtLog.Instance("prefix").Debug("message"); と書けるようにした。
     */
    public static UtLog Instance(string prefix) {
        return new UtLog(prefix);
    }
    public static UtLog Instance(Type type) {
        return new UtLog(type);
    }

    string _prefix;
    public UtLog(string prefix) {
        _prefix = prefix;
    }
    public UtLog(Type clazz) {
        _prefix = clazz.Name;
    }
    string composeMessage(string msg, string memberName, int sourceLineNumber) {
        //var time = $"{DateTime.Now.ToLocalTime()}";
        return $"{DateTime.Now.ToLocalTime()} {_prefix}.{memberName}({sourceLineNumber}) {msg}";
    }

    public void Debug(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        var m = composeMessage(message, memberName, sourceLineNumber);
        LogWrapper.Log(Level.Debug, m);
    }
    public void Info(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.Log(Level.Info, composeMessage(message, memberName, sourceLineNumber));
    }
    public void Warn(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.Log(Level.Warn, composeMessage(message, memberName, sourceLineNumber));
    }
    public void Error(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.Log(Level.Error, composeMessage(message, memberName, sourceLineNumber));
    }
    public void Error(Exception exception, string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.LogError(exception, composeMessage(message, memberName, sourceLineNumber));
    }
    public void Fatal(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.Log(Level.Fatal, composeMessage(message, memberName, sourceLineNumber));
    }

    public void Log(Level level, string message, [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        LogWrapper.Log(level, composeMessage(message, memberName, sourceLineNumber));
    }

    void Chronos(UtLog.Level level, string message, Action action) {
        var time = DateTime.Now;
        try {
            action();
        }
        finally {
            var elapsed = DateTime.Now - time;
            Log(level, $"{message} {elapsed}");
        }
    }
    T Chronos<T>(UtLog.Level level, string message, Func<T> func) {
        var time = DateTime.Now;
        try {
            return func();
        }
        finally {
            var elapsed = DateTime.Now - time;
            Log(level, $"{message} {elapsed}");
        }
    }

    void Chronos(string message, Action action) => Chronos(UtLog.Level.Debug, message, action);
    T Chronos<T>(string message, Func<T> func) => Chronos(UtLog.Level.Debug, message, func);
}

public class Chronos(UtLog logger, UtLog.Level level=UtLog.Level.Debug) { 
    DateTime Time = DateTime.Now;
    UtLog.Level Level = level;
    public void Start(string? message=null) {
        if (message != null) logger.Log(Level, message);
        Time = DateTime.Now;
    }

    public void Lap(string message) {
        var elapsed = DateTime.Now - Time;
        logger.Log(Level, $"{message} {elapsed.Seconds}.{elapsed.Milliseconds} sec");
    }
}
