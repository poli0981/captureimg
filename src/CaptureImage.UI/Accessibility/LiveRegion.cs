// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace CaptureImage.UI.Accessibility;

/// <summary>
/// Makes a screen reader read a status text when it changes. WPF never raises
/// <c>LiveRegionChanged</c> on its own, so <c>AutomationProperties.LiveSetting</c> alone stays
/// silent: with <c>IsEnabled</c> set, the element raises it when a binding with
/// <c>NotifyOnTargetUpdated=True</c> updates its text and when it becomes visible. Changes in
/// quick succession are announced once.
/// </summary>
public static class LiveRegion
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(LiveRegion),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty IsPendingProperty = DependencyProperty.RegisterAttached(
        "IsPending",
        typeof(bool),
        typeof(LiveRegion),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>Announce <paramref name="element"/> (its accessible name) on the next idle moment.</summary>
    public static void Announce(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if ((bool)element.GetValue(IsPendingProperty))
        {
            return;
        }

        element.SetValue(IsPendingProperty, true);
        element.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            element.SetValue(IsPendingProperty, false);
            if (!element.IsVisible
                || element is TextBlock { Text.Length: 0 }
                || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                return;
            }

            UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.RemoveHandler(Binding.TargetUpdatedEvent, (EventHandler<DataTransferEventArgs>)OnTargetUpdated);
        element.IsVisibleChanged -= OnIsVisibleChanged;
        if (e.NewValue is true)
        {
            element.AddHandler(Binding.TargetUpdatedEvent, (EventHandler<DataTransferEventArgs>)OnTargetUpdated);
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
    }

    private static void OnTargetUpdated(object? sender, DataTransferEventArgs e)
    {
        if (ReferenceEquals(e.TargetObject, sender) && e.Property == TextBlock.TextProperty && sender is UIElement element)
        {
            Announce(element);
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is UIElement element)
        {
            Announce(element);
        }
    }
}
