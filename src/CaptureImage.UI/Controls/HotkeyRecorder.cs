// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Input;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Settings;

namespace CaptureImage.UI.Controls;

/// <summary>
/// Attached to the hotkey button in Settings: while its <see cref="HotkeyBindingViewModel"/>
/// is recording, key presses are turned into a binding. Esc cancels, Backspace/Delete clear;
/// Alt combinations arrive as <see cref="Key.System"/> and Print Screen only as a key-up.
/// </summary>
public static class HotkeyRecorder
{
    private const uint VkSnapshot = 0x2C;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(HotkeyRecorder),
        new PropertyMetadata(false, OnIsEnabledChanged));

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

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.PreviewKeyDown -= OnPreviewKeyDown;
        element.PreviewKeyUp -= OnPreviewKeyUp;
        element.LostKeyboardFocus -= OnLostKeyboardFocus;
        if (e.NewValue is true)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
            element.PreviewKeyUp += OnPreviewKeyUp;
            element.LostKeyboardFocus += OnLostKeyboardFocus;
        }
    }

    private static HotkeyBindingViewModel? Recording(object sender) =>
        (sender as FrameworkElement)?.DataContext is HotkeyBindingViewModel { IsRecording: true } vm ? vm : null;

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Recording(sender) is not { } vm)
        {
            return;
        }

        e.Handled = true;
        var key = ActualKey(e);
        var modifiers = CurrentModifiers();
        if (modifiers == HotkeyModifiers.None && key == Key.Escape)
        {
            vm.CancelRecordingCommand.Execute(null);
        }
        else if (modifiers == HotkeyModifiers.None && key is Key.Back or Key.Delete)
        {
            vm.ClearCommand.Execute(null);
        }
        else if (key != Key.Snapshot)
        {
            vm.Submit(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
        }
    }

    private static void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        // Windows never delivers a key-down for Print Screen.
        if (Recording(sender) is { } vm && ActualKey(e) == Key.Snapshot)
        {
            e.Handled = true;
            vm.Submit(CurrentModifiers(), VkSnapshot);
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Recording(sender)?.CancelRecordingCommand.Execute(null);

    private static Key ActualKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };

    private static HotkeyModifiers CurrentModifiers()
    {
        var keyboard = Keyboard.Modifiers;
        var modifiers = HotkeyModifiers.None;
        if (keyboard.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }
        if (keyboard.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }
        if (keyboard.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }
        if (keyboard.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Win;
        }
        return modifiers;
    }
}
