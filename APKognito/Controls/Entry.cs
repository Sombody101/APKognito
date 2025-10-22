using System.Windows.Markup;

namespace APKognito.Controls;

[ContentProperty("Body")]
public class Entry : Control
{
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body),
        typeof(object),
        typeof(Entry)
    );

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(Entry)
    );

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    static Entry()
    {
        // Override the default style key to tell WPF where to find the default style for this control
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Entry), new FrameworkPropertyMetadata(typeof(Entry)));
    }
}
