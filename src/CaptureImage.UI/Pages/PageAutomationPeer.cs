// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace CaptureImage.UI.Pages;

/// <summary>
/// WPF gives <see cref="Page"/> no automation peer, so a page's automation id and title never
/// reach UI Automation. This exposes each page as a pane named after its title, which screen
/// readers announce as a region and UI tests can find.
/// </summary>
internal sealed class PageAutomationPeer(Page owner) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? ((Page)Owner).Title ?? string.Empty : name;
    }
}
