using APKognito.Utilities;
using Microsoft.Win32;
using System.IO;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectorySelector.xaml
/// </summary>
public partial class DirectorySelector
{
    new public event WPF::Input.KeyEventHandler? KeyUp;

    public static readonly DependencyProperty DirectoryPathProperty =
        DependencyProperty.Register(
            name: "DirectoryPath",
            propertyType: typeof(string),
            ownerType: typeof(DirectorySelector),
            typeMetadata: new FrameworkPropertyMetadata(
                defaultValue: string.Empty,
                flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: new PropertyChangedCallback(DirectoryPath_Changed)
            )
        );

    public string DirectoryPath
    {
        get => (string)GetValue(DirectoryPathProperty);
        set => SetValue(DirectoryPathProperty, value);
    }

    public DirectorySelector()
    {
        InitializeComponent();
    }

    private void DirectoryTextBox_KeyUp(object? sender, WPF::Input.KeyEventArgs e)
    {
        KeyUp?.Invoke(sender, e);
    }

    private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
    {
        string? oldOutput = DirectoryPath;

        if (!Directory.Exists(oldOutput))
        {
            oldOutput = null;
        }

        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = false,
            DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (openFolderDialog.ShowDialog() is false
            && openFolderDialog.FolderNames.Length is 0)
        {
            return;
        }

        DirectoryPath = openFolderDialog.FolderName;
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
