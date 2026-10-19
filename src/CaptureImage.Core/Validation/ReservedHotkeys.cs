// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Validation;

/// <summary>
/// Combinations owned by Windows itself. Registering them either fails or steals a system
/// shortcut users rely on, so the hotkey recorder rejects them up front.
/// </summary>
public static class ReservedHotkeys
{
    private const uint VkPrintScreen = 0x2C;

    private static readonly HotkeyBinding[] ReservedList =
    [
        // Lock / session
        new(HotkeyModifiers.Win, 0x4C),                                 // Win+L  — lock workstation
        // Shell navigation
        new(HotkeyModifiers.Win, 0x44),                                 // Win+D  — show desktop
        new(HotkeyModifiers.Win, 0x45),                                 // Win+E  — File Explorer
        new(HotkeyModifiers.Win, 0x52),                                 // Win+R  — Run dialog
        new(HotkeyModifiers.Win, 0x49),                                 // Win+I  — Settings
        new(HotkeyModifiers.Win, 0x58),                                 // Win+X  — power menu
        new(HotkeyModifiers.Win, 0x4D),                                 // Win+M  — minimize all
        new(HotkeyModifiers.Win, 0x09),                                 // Win+Tab — Task View
        new(HotkeyModifiers.Win, 0x47),                                 // Win+G  — Game Bar
        new(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x52),           // Win+Alt+R — Game Bar recording
        // Window snapping
        new(HotkeyModifiers.Win, 0x25),                                 // Win+Left
        new(HotkeyModifiers.Win, 0x26),                                 // Win+Up
        new(HotkeyModifiers.Win, 0x27),                                 // Win+Right
        new(HotkeyModifiers.Win, 0x28),                                 // Win+Down
        // Built-in screenshot shortcuts
        new(HotkeyModifiers.None, VkPrintScreen),                       // PrtScn — Snipping Tool (Windows 11 default)
        new(HotkeyModifiers.Alt, VkPrintScreen),                        // Alt+PrtScn — active window to clipboard
        new(HotkeyModifiers.Win, VkPrintScreen),                        // Win+PrtScn — save screenshot
        new(HotkeyModifiers.Win | HotkeyModifiers.Alt, VkPrintScreen),  // Win+Alt+PrtScn — Game Bar screenshot
        new(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x53),         // Win+Shift+S — Snipping Tool region
        // Task Manager / Security
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x1B),     // Ctrl+Shift+Esc
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x2E),       // Ctrl+Alt+Del (also intercepted by kernel)
        // Window management owned by Alt
        new(HotkeyModifiers.Alt, 0x09),                                 // Alt+Tab
        new(HotkeyModifiers.Alt, 0x73),                                 // Alt+F4
    ];

    public static bool IsReserved(HotkeyBinding binding) => Array.IndexOf(ReservedList, binding) >= 0;
}
