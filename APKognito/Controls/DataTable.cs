using System.Windows.Media;

namespace APKognito.Controls;

public class DataTable : Panel
{
    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(DataTable),
        new FrameworkPropertyMetadata(
            10d,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange
        )
    );

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    private readonly List<RowLayoutInfo> _rowLayouts = [];
    private double _maxHeaderWidth = 0;

    public DataTable()
    {
    }

    protected override int VisualChildrenCount
    {
        get
        {
            int count = 0;
            foreach (RowLayoutInfo info in _rowLayouts)
            {
                if (info.HeaderPresenter is not null)
                {
                    count++;
                }

                if (info.BodyPresenter is not null)
                {
                    count++;
                }

                if (info.OriginalChild is not Entry)
                {
                    count++;
                }
            }
            return count;
        }
    }

    protected override Visual GetVisualChild(int index)
    {
        int current = 0;
        foreach (RowLayoutInfo info in _rowLayouts)
        {
            if (info.HeaderPresenter is not null)
            {
                if (current == index)
                {
                    return info.HeaderPresenter;
                }

                current++;
            }

            if (info.BodyPresenter is not null)
            {
                if (current == index)
                {
                    return info.BodyPresenter;
                }

                current++;
            }

            if (info.OriginalChild is not Entry)
            {
                if (current == index)
                {
                    return info.OriginalChild;
                }

                current++;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        for (int i = VisualChildrenCount - 1; i >= 0; i--)
        {
            RemoveVisualChild(GetVisualChild(i));
        }

        _rowLayouts.Clear();
        _maxHeaderWidth = 0;
        double totalWantedHeight = 0;
        double columnSpacing = ColumnSpacing;

        foreach (UIElement child in InternalChildren)
        {
            var info = new RowLayoutInfo
            {
                OriginalChild = child
            };

            switch (child)
            {
                case Entry entry:
                {
                    info.HeaderPresenter = new ContentPresenter { Content = entry.Header, };
                    info.BodyPresenter = new ContentPresenter { Content = entry.Body };

                    AddVisualChild(info.HeaderPresenter);
                    AddVisualChild(info.BodyPresenter);

                    info.HeaderPresenter.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                    info.BodyPresenter.Measure(new Size(double.PositiveInfinity, availableSize.Height));

                    _maxHeaderWidth = Math.Max(_maxHeaderWidth, info.HeaderPresenter.DesiredSize.Width);
                    totalWantedHeight += Math.Max(info.HeaderPresenter.DesiredSize.Height, info.BodyPresenter.DesiredSize.Height);
                }
                break;

                case Separator separator:
                {
                    separator.Measure(availableSize);
                    totalWantedHeight += separator.DesiredSize.Height;
                    separator.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                break;

                default:
                    throw new UnexpectedControlException(child.GetType());
            }

            totalWantedHeight += 5;
            _rowLayouts.Add(info);
        }

        double wantedWidth = _maxHeaderWidth + columnSpacing;
        if (double.IsInfinity(availableSize.Width))
        {
            wantedWidth += 200;
        }
        else
        {
            wantedWidth = availableSize.Width;
        }

        return new Size(wantedWidth, totalWantedHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double currentY = 0;
        double columnSpacing = ColumnSpacing;

        double actualMaxHeaderWidth = _maxHeaderWidth;

        foreach (RowLayoutInfo info in _rowLayouts)
        {
            if (info.OriginalChild is Entry)
            {
                ContentPresenter header = info.HeaderPresenter;
                ContentPresenter body = info.BodyPresenter;

                double rowHeight = Math.Max(header.DesiredSize.Height, body.DesiredSize.Height);

                header.Arrange(new Rect(0, currentY, actualMaxHeaderWidth, rowHeight));

                double bodyColumnWidth = Math.Max(0, finalSize.Width - actualMaxHeaderWidth - columnSpacing);
                body.Arrange(new Rect(actualMaxHeaderWidth + columnSpacing, currentY, bodyColumnWidth, rowHeight));

                currentY += rowHeight;
            }
            else
            {
                UIElement directChild = info.OriginalChild;
                directChild.Arrange(new Rect(0, currentY, finalSize.Width, directChild.DesiredSize.Height));
                currentY += directChild.DesiredSize.Height;
            }

            currentY += 5;
        }

        return finalSize;
    }

    private sealed class RowLayoutInfo
    {
        public UIElement OriginalChild { get; set; } = null!;
        public ContentPresenter HeaderPresenter { get; set; } = null!;
        public ContentPresenter BodyPresenter { get; set; } = null!;
    }

    public sealed class UnexpectedControlException(Type controlType) : Exception($"Unexpected control of type `{controlType.Name}` added to DataTable. The only allowed types are Entry and Separator.")
    {
    }
}
