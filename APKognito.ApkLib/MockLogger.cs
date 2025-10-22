using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib;

internal sealed class MockLogger : ILogger
{
    public static readonly MockLogger Instance = new();

    private MockLogger()
    {
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullDisposable.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return false;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Left empty intentionally.
    }

    public static ILogger MockIfNull(ILogger? logger)
    {
        return logger ?? Instance;
    }

    internal sealed class NullDisposable : IDisposable
    {
        [SuppressMessage("Critical Code Smell", "S3218:Inner class members should not shadow outer class \"static\" or type members", Justification = "Parent 'Instance' is unused.")]
        public static readonly NullDisposable Instance = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
