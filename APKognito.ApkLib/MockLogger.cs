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
        return logger ?? new MockLogger();
    }
}

internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();

    private NullDisposable()
    {
    }

    public void Dispose()
    {
    }
}
