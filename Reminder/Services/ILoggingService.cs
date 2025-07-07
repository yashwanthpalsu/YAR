using Serilog;
using ILogger = Serilog.ILogger;

namespace Reminder.Services
{
    public interface ILoggingService
    {
        ILogger Logger { get; }
        
        void LogInformation(string message, params object[] propertyValues);
        void LogWarning(string message, params object[] propertyValues);
        void LogError(Exception exception, string message, params object[] propertyValues);
        void LogError(string message, params object[] propertyValues);
        void LogDebug(string message, params object[] propertyValues);
        void LogFatal(Exception exception, string message, params object[] propertyValues);
        
        IDisposable PushProperty(string propertyName, object value);
    }
} 