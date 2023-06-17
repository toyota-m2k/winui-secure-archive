using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace SecureArchive.Utils;
public static class LogUtils {
    static string composeMessage(string msg, string filePath, string memberName, int sourceLineNumber) {
        var index = filePath.LastIndexOf(@"\");
        if(0<index && index<filePath.Length-1) filePath = filePath.Substring(index+1);
        return $"{DateTime.Now.ToLocalTime()}: {filePath}: {memberName}({sourceLineNumber}) {msg}";
    }

    public static void Debug(this ILogger logger, string message="", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogDebug(composeMessage(message, filePath, memberName, sourceLineNumber));
    }
    public static void Info(this ILogger logger, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogInformation(composeMessage(message, filePath, memberName, sourceLineNumber));
    }
    public static void Warn(this ILogger logger, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogWarning(composeMessage(message, filePath, memberName, sourceLineNumber));
    }
    public static void Error(this ILogger logger, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogError(composeMessage(message, filePath, memberName, sourceLineNumber));
    }
    public static void Error(this ILogger logger, Exception exception, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogError(exception, composeMessage(message, filePath, memberName, sourceLineNumber));
    }
    public static void Fatal(this ILogger logger, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        logger.LogCritical(composeMessage(message, filePath, memberName, sourceLineNumber));
    }
}

public class UtLog {
    static private ILogger _globalLogger = null!;
    public static void SetGlobalLogger(ILogger logger) {
        _globalLogger = logger;
    }
    ILogger _logger;
    string _prefix;
    public UtLog(string prefix, ILogger? logger=null) {
        _prefix = prefix;
        _logger = logger ?? _globalLogger;
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
        _logger.LogDebug(m);
    }
    public void Info(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        _logger.LogInformation(composeMessage(message, memberName, sourceLineNumber));
    }
    public void Warn(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        _logger.LogWarning(composeMessage(message, memberName, sourceLineNumber));
    }
    public void Error(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        _logger.LogError(composeMessage(message, memberName, sourceLineNumber));
    }
    public void Error(Exception exception, string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        _logger.LogError(exception, composeMessage(message, memberName, sourceLineNumber));
    }
    public void Fatal(string message = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0) {
        _logger.LogCritical(composeMessage(message, memberName, sourceLineNumber));
    }
}
