// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using CaptureImage.ViewModels.Settings;

namespace CaptureImage.UI.Pages;

public partial class SettingsPage : Page, IActivatablePage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public void SetActive(bool active)
    {
        if (!active)
        {
            // Never leave the capture hotkeys suspended behind a hidden page.
            foreach (var hotkey in ViewModel.Hotkeys)
            {
                hotkey.CancelRecordingCommand.Execute(null);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);
}
