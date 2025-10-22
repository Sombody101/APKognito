using System.Collections.Specialized;
using System.Windows.Documents;
using System.Windows.Media;
using APKognito.Models;
using Wpf.Ui.Controls;
using ElementCollection = System.Collections.ObjectModel.ObservableCollection<object>;
using LogEntryType = APKognito.Models.LogBoxEntry.LogEntryType;

namespace APKognito.Controls;

public static class RichTextBoxLogBehavior
{
    public static readonly DependencyProperty LogEntriesProperty =
        DependencyProperty.RegisterAttached(
            "LogEntries",
            typeof(ElementCollection),
            typeof(RichTextBoxLogBehavior),
            new PropertyMetadata(null, OnLogEntriesChanged));

    public static ElementCollection GetLogEntries(DependencyObject obj)
    {
        return (ElementCollection)obj.GetValue(LogEntriesProperty);
    }

    public static void SetLogEntries(DependencyObject obj, ElementCollection value)
    {
        obj.SetValue(LogEntriesProperty, value);
    }

    public static readonly DependencyProperty LogIconPrefixesProperty =
        DependencyProperty.RegisterAttached(
            "LogIconPrefixes",
            typeof(bool),
            typeof(RichTextBoxLogBehavior),
            new PropertyMetadata(true));

    public static bool GetLogIconPrefixes(DependencyObject obj)
    {
        return (bool)obj.GetValue(LogIconPrefixesProperty);
    }

    public static void SetLogIconPrefixes(DependencyObject obj, bool value)
    {
        obj.SetValue(LogIconPrefixesProperty, value);
    }

    private static void OnLogEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox textBox)
        {
            return;
        }

        if (e.OldValue is ElementCollection oldCollection)
        {
            oldCollection.CollectionChanged -= TextboxUpdateHandler;
        }

        if (e.NewValue is ElementCollection newCollection)
        {
            newCollection.CollectionChanged += TextboxUpdateHandler;

            if (textBox.Document is null)
            {
                textBox.Document = new FlowDocument();
                textBox.Document.Blocks.Add(new Paragraph());
            }

            if (newCollection.Count > 0 && textBox.Document?.Blocks.FirstOrDefault() is Paragraph initialParagraph)
            {
                _ = textBox.Dispatcher.BeginInvoke(() => PopulateRichTextBox(initialParagraph, newCollection, GetLogIconPrefixes(textBox)));
            }
        }

        void TextboxUpdateHandler(object? sender, NotifyCollectionChangedEventArgs args)
        {
            textBox.Dispatcher.BeginInvoke(() => UpdateRichTextBox(textBox, args));
        }
    }

    private static void PopulateRichTextBox(Paragraph paragraph, ElementCollection entries, bool logIconPrefixes)
    {
        paragraph.Inlines.Clear();
        foreach (object entry in entries)
        {
            AddToParagraph(paragraph, entry, logIconPrefixes);
        }
    }

    private static void UpdateRichTextBox(RichTextBox textBox, NotifyCollectionChangedEventArgs args)
    {
        if (!textBox.Dispatcher.CheckAccess())
        {
            _ = textBox.Dispatcher.BeginInvoke(() => UpdateRichTextBox(textBox, args), System.Windows.Threading.DispatcherPriority.Background);
        }

        if (textBox.Document?.Blocks.LastOrDefault() is not Paragraph paragraph)
        {
            paragraph = new Paragraph();
            textBox.Document?.Blocks.Add(paragraph);
        }

        bool logIconPrefixes = GetLogIconPrefixes(textBox);

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.NewItems is not null)
                {
                    foreach (object newItem in args.NewItems)
                    {
                        AddToParagraph(paragraph, newItem, logIconPrefixes);
                    }

                    ScrollToEndIfAtBottom(textBox);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                textBox.Document?.Blocks.Clear();
                break;
        }
    }

    private static void AddToParagraph(Paragraph paragraph, object newItem, bool? logIconPrefixes)
    {
        switch (newItem)
        {
            case LogBoxEntry logEntry:
                AddLogEntryToParagraph(paragraph, logEntry.Text, logEntry.Color, logEntry.LogType, logIconPrefixes ?? false);
                break;

            case UIElement element:
                AddElementToParagraph(paragraph, element);
                break;

            case Block block:
                (paragraph.Parent as FlowDocument)!.Blocks.Add(block);
                break;

            default:
                AddUnknownToParagraph(paragraph, newItem);
                break;
        }
    }

    private static void AddLogEntryToParagraph(Paragraph paragraph, string text, Brush? color, LogEntryType? logType, bool logIconPrefixes)
    {
        if (logIconPrefixes && logType is not (null or LogEntryType.None))
        {
            SymbolRegular symbol = SymbolRegular.Empty;

            switch (logType)
            {
                case LogEntryType.Info:
                    symbol = SymbolRegular.Info16;
                    break;

                case LogEntryType.Success:
                    symbol = SymbolRegular.CheckmarkCircle32;
                    break;

                case LogEntryType.Warning:
                    symbol = SymbolRegular.Warning16;
                    break;

                case LogEntryType.Error:
                    symbol = SymbolRegular.ErrorCircle24;
                    break;

                case LogEntryType.Debug:
                    symbol = SymbolRegular.Bug16;
                    break;
            }

            SymbolIcon icon = new()
            {
                Symbol = symbol,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new(0, 0, 5, 0)
            };

            if (color is not null)
            {
                icon.Foreground = color;
            }

            paragraph.Inlines.Add(new InlineUIContainer(icon));
        }

        Run log = new(text)
        {
            BaselineAlignment = BaselineAlignment.Center,
        };

        if (color is not null)
        {
            log.Foreground = color;
        }

        paragraph.Inlines.Add(log);
    }

    private static void AddElementToParagraph(Paragraph paragraph, UIElement element)
    {
        paragraph.Inlines.Add(element);
    }

    private static void AddUnknownToParagraph(Paragraph paragraph, object obj)
    {
        paragraph.Inlines.Add(obj.ToString());
    }

    private static void ScrollToEndIfAtBottom(RichTextBox textBox)
    {
        if (textBox.VerticalOffset + textBox.ViewportHeight >= textBox.ExtentHeight)
        {
            textBox.ScrollToEnd();
        }
    }
}
