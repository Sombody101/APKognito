using System.Diagnostics;
using System.Windows.Threading;

namespace APKognito.Utilities;

public sealed class FrameLockDetector : IDisposable
{
    private const double MIN_FPS = 1.75;

    private readonly TimeSpan _maxFrameTime = TimeSpan.FromMilliseconds(1000.0 / MIN_FPS);

    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _frameWatch = new();
    private bool _firstFrame = true;

    public FrameLockDetector(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _dispatcher.Hooks.DispatcherInactive += DispatcherInactiveHandler;
        _frameWatch.Start();
    }

    private void DispatcherInactiveHandler(object? sender, EventArgs e)
    {
        if (_firstFrame)
        {
            _firstFrame = false;
            _frameWatch.Restart();
            return;
        }

        _frameWatch.Stop();

        TimeSpan frameTime = _frameWatch.Elapsed;

        if (frameTime > _maxFrameTime)
        {
            double actualFps = 1000.0 / frameTime.TotalMilliseconds;
            FileLogger.LogWarning($"UI frame time flagged over {_maxFrameTime.TotalMilliseconds:F0}ms, took {frameTime.TotalMilliseconds:F0}ms (actual FPS: {actualFps:F1})");
        }

        _frameWatch.Restart();
    }

    public void Dispose()
    {
        _dispatcher.Hooks.DispatcherInactive -= DispatcherInactiveHandler;
        _frameWatch.Stop();
    }
}
