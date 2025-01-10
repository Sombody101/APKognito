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

    public static readonly DependencyProperty SelectingDirectoryProperty =
    DependencyProperty.Register(
        name: "SelectingDirectory",
        propertyType: typeof(bool),
        ownerType: typeof(DirectorySelector),
        typeMetadata: new FrameworkPropertyMetadata(
            defaultValue: true,
            flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
        )
    );

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
