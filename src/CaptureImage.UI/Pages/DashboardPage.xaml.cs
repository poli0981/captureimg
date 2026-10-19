// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Controls;
using CaptureImage.ViewModels.Dashboard;

namespace CaptureImage.UI.Pages;

public partial class DashboardPage : Page, IActivatablePage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public DashboardViewModel ViewModel { get; }

    public void SetActive(bool active) => ViewModel.IsPageActive = active;
}
