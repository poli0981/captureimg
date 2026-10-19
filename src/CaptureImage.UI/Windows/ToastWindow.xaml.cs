// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CaptureImage.Core.Models;
using CaptureImage.UI.Interop;
using CaptureImage.UI.Localization;
using Wpf.Ui.Controls;

namespace CaptureImage.UI.Windows;

/// <summary>
/// A notification pop-up for when the main window isn't in front: never activated (a click
/// doesn't steal focus from a game) and excluded from screen capture.
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastWindow(ToastKind kind, string title, string message, SymbolRegular symbol, TimeSpan timeout)
    {
        InitializeComponent();
        LocalizationSource.Attach(this);
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        KindIcon.Symbol = symbol;
        if (TryFindResource(kind switch
        {
            ToastKind.Success => "SystemFillColorSuccessBrush",
            ToastKind.Warning => "SystemFillColorCautionBrush",
            ToastKind.Error => "SystemFillColorCriticalBrush",
            _ => "AccentTextFillColorPrimaryBrush",
        }) is Brush accent)
        {
            KindIcon.Foreground = accent;
        }
        AutomationProperties.SetName(this, string.IsNullOrEmpty(message) ? title : $"{title}. {message}");

        _timer = new DispatcherTimer { Interval = timeout };
        _timer.Tick += (_, _) => Close();
        SourceInitialized += (_, _) =>
        {
            WindowInterop.MakeNonActivating(this);
            WindowInterop.ExcludeFromCapture(this);
        };
        Loaded += (_, _) =>
        {
            _timer.Start();
            UIElementAutomationPeer.FromElement(MessageText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        };
        Closed += (_, _) => _timer.Stop();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _timer.Stop();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _timer.Start();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
