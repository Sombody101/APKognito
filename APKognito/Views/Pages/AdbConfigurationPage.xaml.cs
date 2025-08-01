﻿using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for AdbConfigurationPage.xaml
/// </summary>
public partial class AdbConfigurationPage : INavigableView<AdbConfigurationViewModel>, IViewable
{
    public AdbConfigurationViewModel ViewModel { get; }

    public AdbConfigurationPage()
    {
        // For designer
        DataContext = this;
        ViewModel = new();
    }

    public AdbConfigurationPage(AdbConfigurationViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
