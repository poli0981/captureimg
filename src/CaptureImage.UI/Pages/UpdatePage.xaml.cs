// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Controls;
using CaptureImage.ViewModels.Update;

namespace CaptureImage.UI.Pages;

public partial class UpdatePage : Page, IActivatablePage
{
    public UpdatePage(UpdateViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public UpdateViewModel ViewModel { get; }

    public void SetActive(bool active) => ViewModel.IsPageActive = active;
}
