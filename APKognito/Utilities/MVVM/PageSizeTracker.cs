using System.Windows.Interop;

namespace APKognito.Utilities.MVVM;

[Obsolete]
public class PageSizeTracker : ObservableObject
{
    public const int TitlebarHeight = 50;

    private FrameworkElement _page;
    private Window _window;

    private bool _isMaximized = false;
    private double _maximizedHeight;
    private double _maximizedWidth;

    public event EventHandler<SizeChangedEventArgs>? PageSizeChanged;
    public event EventHandler<SizeChangedEventArgs>? WindowSizeChanged;

    public void SetAndInitializePageSize(FrameworkElement page)
    {
        _page = page;
        page.SizeChanged += (sender, e) =>
        {
            PageSizeChanged?.Invoke(sender, e);
        };

        _window = Window.GetWindow(page);
        _window.SizeChanged += (sender, e) =>
        {
            if (e.NewSize == e.PreviousSize)
            {
                return;
            }

            WindowSizeChanged?.Invoke(sender, e);
        };

        // Set the max size if the window was maximized when navigating to the page
        if (_window.WindowState == WindowState.Maximized)
        {
            _isMaximized = true;
            SetMaximizedSize();
        }

        _window.StateChanged += (sender, e) =>
        {
            bool maximized = _window.WindowState == WindowState.Maximized;

            if (maximized == _isMaximized)
            {
                return;
            }

            _isMaximized = maximized;

            if (maximized)
            {
                SetMaximizedSize();
            }

            WindowSizeChanged?.Invoke(sender, null!);
        };

        PageSizeChanged?.Invoke(this, null!);
        WindowSizeChanged?.Invoke(this, null!);
    }

    public double PageHeight => _page.Height;
    public double PageWidth => _page.Width;

    public double WindowHeight => _isMaximized ? _maximizedHeight : _window.Height;
    public double WindowWidth => _isMaximized ? _maximizedWidth : _window.Width;

    public double GetPageHeightPercent(double percent)
    {
        return _page.Height * (percent / 100);
    }

    public double GetPageWidthPercent(double percent)
    {
        return _page.Width * (percent / 100);
    }

    public double GetWindowHeightPercent(double percent)
    {
        return _window.Height * (percent / 100);
    }

    public double GetWindowWidthPercent(double percent)
    {
        return _window.Width * (percent / 100);
    }

    private void SetMaximizedSize()
    {
         Screen screen = Screen.FromHandle(new WindowInteropHelper(_window).Handle);
        _maximizedHeight = screen.Bounds.Height;
        _maximizedWidth = screen.Bounds.Width;
    }
}
