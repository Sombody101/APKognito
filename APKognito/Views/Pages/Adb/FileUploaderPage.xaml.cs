﻿using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for FileUploader.xaml
/// </summary>
public partial class FileUploaderPage : INavigableView<FileUploaderViewModel>, IViewable
{
    public FileUploaderViewModel ViewModel { get; }

    public FileUploaderPage(FileUploaderViewModel viewModel)
    {
        InitializeComponent();
        DataContext = this;
        ViewModel = viewModel;
    }
}