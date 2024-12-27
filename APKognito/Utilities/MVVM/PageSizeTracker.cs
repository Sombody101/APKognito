namespace APKognito.Utilities;

public class PageSizeTracker : ObservableObject
{
    public const int TitlebarHeight = 50;

    private FrameworkElement _page;
    private Window _window;

    public event EventHandler<SizeChangedEventArgs> PageSizeChanged;
    public event EventHandler<SizeChangedEventArgs> WindowSizeChanged;

    public void SetPage(FrameworkElement page)
    {
        _page = page;
        page.SizeChanged += (sender, e) =>
        {
            PageSizeChanged?.Invoke(sender, e);
        };

        _window = Window.GetWindow(page);
        _window.SizeChanged += (sender, e) =>
        {
            WindowSizeChanged?.Invoke(sender, e);
        };

        _window.StateChanged += (sender, e) =>
        {
            WindowSizeChanged?.Invoke(sender, null!);
        };

        PageSizeChanged?.Invoke(this, null!);
        WindowSizeChanged?.Invoke(this, null!);
    }

    public double PageHeight => _page.Height;
    public double PageWidth => _page.Width;

    public double WindowHeight => _window.Height;
    public double WindowWidth => _window.Width;

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
}
