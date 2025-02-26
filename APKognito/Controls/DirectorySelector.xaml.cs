using APKognito.Utilities;
using Microsoft.Win32;
using System.IO;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectorySelector.xaml
/// </summary>
public partial class DirectorySelector
{
    [SuppressMessage("Major Code Smell", "S3264:Events should be invoked", Justification = "Used externally")]
    public new event KeyEventHandler? KeyUp;

    public static readonly DependencyProperty DirectoryPathProperty =
        DependencyProperty.Register(nameof(DirectoryPath), typeof(string), typeof(DirectorySelector),
            new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(DirectoryPath_Changed))
        );

    public static readonly DependencyProperty SelectingDirectoryProperty =
        DependencyProperty.Register(nameof(SelectingDirectory), typeof(bool), typeof(DirectorySelector));

    public static readonly DependencyProperty BrowseButtonIconProperty =
        DependencyProperty.Register(nameof(BrowseButtonIcon), typeof(SymbolIcon), typeof(DirectorySelector));

    public string DirectoryPath
    {
        get => (string)GetValue(DirectoryPathProperty);
        set => SetValue(DirectoryPathProperty, value);
    }

    public bool SelectingDirectory
    {
        get => (bool)GetValue(SelectingDirectoryProperty);
        set => SetValue(SelectingDirectoryProperty, value);
    }

    public SymbolIcon BrowseButtonIcon
    {
        get => (SymbolIcon)GetValue(BrowseButtonIconProperty);
        set => SetValue(BrowseButtonIconProperty, value);
    }

    public DirectorySelector()
    {
        InitializeComponent();
        BrowseButtonIcon ??= new() { Symbol = SymbolRegular.Folder20 };
    }

    private void DirectoryTextBox_KeyUp(object? sender, WPF::Input.KeyEventArgs e)
    {
        TextBox tBox;

        switch (sender)
        {
            case TextBox:
                tBox = (TextBox)sender;
                break;

            case DirectorySelector:
                tBox = ((DirectorySelector)sender).DirectoryTextBox;
                break;

            default:
                return;
        }

        App.ForwardKeystrokeToBinding(tBox);
    }

    private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
    {
        string? oldOutput = DirectoryPath;

        if (!Directory.Exists(oldOutput))
        {
            oldOutput = null;
        }

        if (SelectingDirectory)
        {
            OpenFolderDialog openFolderDialog = new()
            {
                Multiselect = false,
                DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (openFolderDialog.ShowDialog() is false)
            {
                return;
            }

            DirectoryPath = openFolderDialog.FolderName;
            return;
        }

        OpenFileDialog openFileDialog = new()
        {
            Multiselect = false,
            DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (openFileDialog.ShowDialog() is false)
        {
            return;
        }

        DirectoryPath = openFileDialog.FileName;
    }

    private static void DirectoryPath_Changed(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not DirectorySelector selector)
        {
            return;
        }

        string value = (string)e.NewValue;

        selector.DirectoryPath = VariablePathResolver.Resolve(value);
    }
}
