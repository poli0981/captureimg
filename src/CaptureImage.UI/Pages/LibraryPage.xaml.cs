// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using CaptureImage.ViewModels.Library;

namespace CaptureImage.UI.Pages;

public partial class LibraryPage : Page, IActivatablePage
{
    public LibraryPage(LibraryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public LibraryViewModel ViewModel { get; }

    public void SetActive(bool active) => ViewModel.IsPageActive = active;

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.OpenCommand.CanExecute(null))
        {
            ViewModel.OpenCommand.Execute(null);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);
}
