// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace CaptureImage.UI.Accessibility;

/// <summary>
/// Reads a notification to screen readers with a UI Automation notification event; the text
/// travels with the event, so it doesn't depend on what has focus or is visible.
/// </summary>
public static class Announcer
{
    private const string ActivityId = "CaptureImage.Notification";

    /// <summary>Announce <paramref name="text"/> from <paramref name="source"/>.</summary>
    /// <param name="source">An element of the window the notification belongs to.</param>
    /// <param name="text">What to read.</param>
    /// <param name="important">Errors: read even when the user is busy, replacing older notifications.</param>
    public static void Announce(UIElement source, string text, bool important = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(text) || !AutomationPeer.ListenerExists(AutomationEvents.Notification))
        {
            return;
        }

        UIElementAutomationPeer.CreatePeerForElement(source)?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            important ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
            text,
            ActivityId);
    }

    /// <summary>"Title. Message", or just the title when there is no message.</summary>
    public static string Compose(string title, string? message) =>
        string.IsNullOrEmpty(message) ? title : $"{title}. {message}";
}
