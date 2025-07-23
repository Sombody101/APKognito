namespace APKognito.Controls.Overrides;

public class Card : ContentControl
{
    /// <summary>Identifies the <see cref="Footer"/> dependency property.</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer),
        typeof(object),
        typeof(Card),
        new PropertyMetadata(null, OnFooterChanged)
    );

    /// <summary>Identifies the <see cref="HasFooter"/> dependency property.</summary>
    public static readonly DependencyProperty HasFooterProperty = DependencyProperty.Register(
        nameof(HasFooter),
        typeof(bool),
        typeof(Card),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(Card),
        new PropertyMetadata(new CornerRadius(4))
    );

    /// <summary>
    /// Gets or sets additional content displayed at the bottom.
    /// </summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="Card"/> has a <see cref="Footer"/>.
    /// </summary>
    public bool HasFooter
    {
        get => (bool)GetValue(HasFooterProperty);
        internal set => SetValue(HasFooterProperty, value);
    }

    /// <summary>
    /// Gets or set the <see cref="Card"/> <see cref="System.Windows.CornerRadius"/>
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static void OnFooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Card control)
        {
            return;
        }

        control.SetValue(HasFooterProperty, control.Footer != null);
    }
}
