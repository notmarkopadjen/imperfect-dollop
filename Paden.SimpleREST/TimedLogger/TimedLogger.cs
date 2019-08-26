using Microsoft.Extensions.Logging;
using System;

namespace Paden.SimpleREST.TimedLogger
{
    public class TimedLogger<T> : ILogger<T>
    {
        ILogger logger;

        public TimedLogger(ILoggerFactory factory)
        {
            logger = factory.CreateLogger<T>();

        }

        IDisposable ILogger.BeginScope<TState>(TState state) => logger.BeginScope(state);

        bool ILogger.IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        => logger.Log(logLevel, eventId, state, exception, (s, e) => $"{DateTime.UtcNow:O} - {formatter(s, e)}");
    }
}
