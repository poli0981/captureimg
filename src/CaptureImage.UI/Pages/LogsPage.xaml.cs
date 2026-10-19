// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Specialized;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using CaptureImage.ViewModels.Logs;

namespace CaptureImage.UI.Pages;

public partial class LogsPage : Page, IActivatablePage
{
    private ScrollViewer? _scroll;

    public LogsPage(LogViewerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ViewModel.Entries.CollectionChanged += OnEntriesChanged;
    }

    public LogViewerViewModel ViewModel { get; }

    public void SetActive(bool active) => ViewModel.IsPageActive = active;

    /// <summary>Follow new entries while the list is scrolled to the bottom.</summary>
    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || ViewModel.Entries.Count == 0)
        {
            return;
        }

        _scroll ??= FindScrollViewer(EntryList);
        if (_scroll is null || _scroll.VerticalOffset >= _scroll.ScrollableHeight - 2)
        {
            EntryList.ScrollIntoView(ViewModel.Entries[^1]);
        }
    }

    private static ScrollViewer? FindScrollViewer(System.Windows.DependencyObject root)
    {
        if (root is ScrollViewer viewer)
        {
            return viewer;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);
}
