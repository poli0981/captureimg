// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Controls;
using CaptureImage.ViewModels.About;

namespace CaptureImage.UI.Pages;

public partial class AboutPage : Page, IActivatablePage
{
    public AboutPage(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public AboutViewModel ViewModel { get; }

    public void SetActive(bool active)
    {
        // Static content.
    }
}
