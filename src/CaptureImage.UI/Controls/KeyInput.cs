// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Input;
using CaptureImage.Core.Models;

namespace CaptureImage.UI.Controls;

/// <summary>Turns WPF key events into the Win32 virtual keys and modifiers hotkeys use.</summary>
internal static class KeyInput
{
    /// <summary>The key that was really pressed: Alt combinations arrive as <see cref="Key.System"/>.</summary>
    public static Key ActualKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };

    public static HotkeyModifiers CurrentModifiers()
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

    /// <summary>The pressed key with the modifiers held, as a hotkey binding.</summary>
    public static HotkeyBinding Binding(KeyEventArgs e) =>
        new(CurrentModifiers(), (uint)KeyInterop.VirtualKeyFromKey(ActualKey(e)));
}
