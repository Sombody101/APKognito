using APKognito.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Controls;

using LogEntryType = APKognito.Models.LogBoxEntry.LogEntryType;

namespace APKognito.Controls;

public static class RichTextBoxLogBehavior
{
    public static readonly DependencyProperty LogEntriesProperty =
        DependencyProperty.RegisterAttached(
            "LogEntries",
            typeof(ObservableCollection<LogBoxEntry>),
            typeof(RichTextBoxLogBehavior),
            new PropertyMetadata(null, OnLogEntriesChanged));

    public static ObservableCollection<LogBoxEntry> GetLogEntries(DependencyObject obj)
    {
        return (ObservableCollection<LogBoxEntry>)obj.GetValue(LogEntriesProperty);
    }

    public static void SetLogEntries(DependencyObject obj, ObservableCollection<LogBoxEntry> value)
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

        if (e.OldValue is ObservableCollection<LogBoxEntry> oldCollection)
        {
            oldCollection.CollectionChanged -= TextboxUpdateHandler;
        }

        if (e.NewValue is ObservableCollection<LogBoxEntry> newCollection)
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

    private static void PopulateRichTextBox(Paragraph paragraph, ObservableCollection<LogBoxEntry> entries, bool logIconPrefixes)
    {
        paragraph.Inlines.Clear();
        foreach (LogBoxEntry entry in entries)
        {
            AddLogEntryToParagraph(paragraph, entry.Text, entry.Color, entry.LogType, logIconPrefixes);
        }
    }

    private static void UpdateRichTextBox(RichTextBox textBox, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
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
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                if (args.NewItems is not null)
                {
                    foreach (LogBoxEntry newItem in args.NewItems)
                    {
                        AddLogEntryToParagraph(paragraph, newItem.Text, newItem.Color, newItem.LogType, logIconPrefixes);
                    }

                    ScrollToEndIfAtBottom(textBox);
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                textBox.Document?.Blocks.Clear();
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

    private static void ScrollToEndIfAtBottom(RichTextBox textBox)
    {
        if (textBox.VerticalOffset + textBox.ViewportHeight >= textBox.ExtentHeight)
        {
            textBox.ScrollToEnd();
        }
    }
}
