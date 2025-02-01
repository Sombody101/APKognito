using APKognito.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Windows.Data;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectorySelector.xaml
/// </summary>
public partial class DirectorySelector
{
    public new event WPF::Input.KeyEventHandler? KeyUp;

    public SymbolIcon BrowseButtonIcon { get; private set; } = new() { Symbol = SymbolRegular.Folder20 };

    public static readonly DependencyProperty DirectoryPathProperty =
        DependencyProperty.Register(nameof(DirectoryPath), typeof(string), typeof(DirectorySelector),
            typeMetadata: new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(DirectoryPath_Changed))
        );

    public static readonly DependencyProperty SelectingDirectoryProperty =
        DependencyProperty.Register(nameof(SelectingDirectory), typeof(bool), typeof(DirectorySelector));

    public string DirectoryPath
    {
        get => (string)GetValue(DirectoryPathProperty);
        set => SetValue(DirectoryPathProperty, value);
    }

    public bool SelectingDirectory
    {
        get => (bool)GetValue(SelectingDirectoryProperty);
        set
        {
            BrowseButtonIcon.Symbol = value
                ? SymbolRegular.Folder48
                : SymbolRegular.Document48;

            SetValue(SelectingDirectoryProperty, value);
        }
    }

    public DirectorySelector()
    {
        InitializeComponent();
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

        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
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
