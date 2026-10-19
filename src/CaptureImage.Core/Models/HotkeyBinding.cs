// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Validation;

namespace CaptureImage.Core.Models;

/// <summary>
/// Outcome of <see cref="HotkeyBinding.Validate"/>. The UI renders an error for anything but
/// <see cref="Ok"/>; only valid (or unbound) bindings are persisted.
/// </summary>
public enum HotkeyValidationResult
{
    /// <summary>Binding is acceptable for use.</summary>
    Ok,

    /// <summary>No primary (non-modifier) key — e.g. only Ctrl pressed.</summary>
    NoPrimaryKey,

    /// <summary><see cref="HotkeyBinding.VirtualKey"/> is itself a modifier VK (Shift/Ctrl/Alt/Win).</summary>
    ModifierOnlyKey,

    /// <summary>Plain letter/digit without any modifier — too easy to trigger while typing.</summary>
    RequiresModifier,

    /// <summary>Combination is owned by the Windows shell (<see cref="ReservedHotkeys"/>).</summary>
    ReservedByWindows,
}

/// <summary>
/// Modifier flags for a global hotkey. Values match the Win32 <c>MOD_*</c> constants so
/// Infrastructure can forward them to <c>RegisterHotKey</c> unchanged.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// A global hotkey: Win32 virtual-key code (<c>VK_*</c>) plus modifiers. A binding with
/// <see cref="VirtualKey"/> 0 is <see cref="Unbound"/> (the mode has no hotkey).
/// </summary>
/// <param name="Modifiers">Required modifier state.</param>
/// <param name="VirtualKey">Primary non-modifier key, Win32 VK code.</param>
public readonly record struct HotkeyBinding(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private const uint VkF10 = 0x79;
    private const uint VkF11 = 0x7A;
    private const uint VkF12 = 0x7B;

    /// <summary>No hotkey.</summary>
    public static HotkeyBinding Unbound => default;

    /// <summary>The v1 default, kept for the Window mode: <c>Ctrl+Shift+F12</c>.</summary>
    public static HotkeyBinding Default => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkF12);

    public bool IsUnbound => VirtualKey == 0;

    /// <summary>Default binding for each capture mode.</summary>
    public static HotkeyBinding DefaultFor(CaptureMode mode) => mode switch
    {
        CaptureMode.Window => Default,
        CaptureMode.Region => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkF11),
        CaptureMode.Monitor => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkF10),
        _ => Unbound,
    };

    /// <summary>
    /// Human-readable form such as <c>Ctrl+Shift+F12</c> (empty for <see cref="Unbound"/>).
    /// Display and logging only — persist the struct fields, not this string.
    /// </summary>
    public override string ToString()
    {
        if (IsUnbound)
        {
            return string.Empty;
        }

        var parts = new List<string>(5);
        if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
        parts.Add(VirtualKeyName(VirtualKey));
        return string.Join("+", parts);
    }

    /// <summary>
    /// Classify this binding for the hotkey recorder. Pure function — does not touch Windows
    /// state; whether another program already owns the combination is checked separately.
    /// </summary>
    public HotkeyValidationResult Validate()
    {
        if (VirtualKey == 0)
            return HotkeyValidationResult.NoPrimaryKey;

        if (IsModifierVk(VirtualKey))
            return HotkeyValidationResult.ModifierOnlyKey;

        // Before the modifier rule: bare PrtScn is reserved, not merely "needs a modifier".
        if (ReservedHotkeys.IsReserved(this))
            return HotkeyValidationResult.ReservedByWindows;

        if (Modifiers == HotkeyModifiers.None && !IsFunctionKey(VirtualKey))
            return HotkeyValidationResult.RequiresModifier;

        return HotkeyValidationResult.Ok;
    }

    public bool IsValid() => Validate() == HotkeyValidationResult.Ok;

    private static bool IsModifierVk(uint vk) => vk is
        0x10 or 0x11 or 0x12 or            // VK_SHIFT, VK_CONTROL, VK_MENU (Alt)
        0x5B or 0x5C or                    // VK_LWIN, VK_RWIN
        0xA0 or 0xA1 or                    // VK_LSHIFT, VK_RSHIFT
        0xA2 or 0xA3 or                    // VK_LCONTROL, VK_RCONTROL
        0xA4 or 0xA5;                      // VK_LMENU,   VK_RMENU

    private static bool IsFunctionKey(uint vk) => vk is >= 0x70 and <= 0x87; // F1..F24

    private static string VirtualKeyName(uint vk) => vk switch
    {
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F).ToString(CultureInfo.InvariantCulture),
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => "Num" + (vk - 0x60).ToString(CultureInfo.InvariantCulture),
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x1B => "Esc",
        0x2C => "PrtScn",
        0x13 => "Pause",
        0x91 => "ScrollLock",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0xC0 => "`",
        0xBD => "-",
        0xBB => "=",
        0xDB => "[",
        0xDD => "]",
        0xDC => "\\",
        0xBA => ";",
        0xDE => "'",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        _ => $"VK_{vk:X2}",
    };
}
