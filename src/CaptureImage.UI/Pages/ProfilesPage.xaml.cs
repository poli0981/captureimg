// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using CaptureImage.ViewModels.Profiles;

namespace CaptureImage.UI.Pages;

public partial class ProfilesPage : Page, IActivatablePage
{
    public ProfilesPage(ProfilesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public ProfilesViewModel ViewModel { get; }

    public void SetActive(bool active)
    {
        // Profiles are edited in place; nothing refreshes in the background.
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);
}
