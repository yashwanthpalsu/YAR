using Serilog;
using Serilog.Context;
using ILogger = Serilog.ILogger;

namespace Reminder.Services
{
    public class LoggingService : ILoggingService
    {
        private readonly ILogger _logger;

        public LoggingService(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger Logger => _logger;

        public void LogInformation(string message, params object[] propertyValues)
        {
            _logger.Information(message, propertyValues);
        }

        public void LogWarning(string message, params object[] propertyValues)
        {
            _logger.Warning(message, propertyValues);
        }

        public void LogError(Exception exception, string message, params object[] propertyValues)
        {
            _logger.Error(exception, message, propertyValues);
        }

        public void LogError(string message, params object[] propertyValues)
        {
            _logger.Error(message, propertyValues);
        }

        public void LogDebug(string message, params object[] propertyValues)
        {
            _logger.Debug(message, propertyValues);
        }

        public void LogFatal(Exception exception, string message, params object[] propertyValues)
        {
            _logger.Fatal(exception, message, propertyValues);
        }

        public IDisposable PushProperty(string propertyName, object value)
        {
            return LogContext.PushProperty(propertyName, value);
        }
    }
} 