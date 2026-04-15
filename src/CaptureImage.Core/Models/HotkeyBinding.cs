using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CaptureImage.Core.Models;

/// <summary>
/// Modifier flags for a global hotkey. Values match the common Win32 MOD_* constants
/// so Infrastructure can forward to <c>RegisterHotKey</c> without re-mapping.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None    = 0,
    Alt     = 0x0001,
    Control = 0x0002,
    Shift   = 0x0004,
    Win     = 0x0008,
}

/// <summary>
/// Keyed on Win32 virtual-key code (<c>VK_*</c>). Stored as <c>uint</c> so persistence
/// stays round-trippable across SharpHook's own keycode enum.
/// </summary>
/// <param name="Modifiers">Required modifier state.</param>
/// <param name="VirtualKey">Primary non-modifier key, Win32 VK code.</param>
public readonly record struct HotkeyBinding(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>
    /// Human-readable form: <c>"Ctrl+Shift+F12"</c>. Used for settings display and logging.
    /// Not a stable wire format — do not persist this string, persist the struct fields.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt)     != 0) parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift)   != 0) parts.Add("Shift");
        if ((Modifiers & HotkeyModifiers.Win)     != 0) parts.Add("Win");
        parts.Add(VirtualKeyName(VirtualKey));
        return string.Join("+", parts);
    }

    /// <summary>The default binding shipped to users on first run: <c>Ctrl+Shift+F12</c>.</summary>
    public static HotkeyBinding Default =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B); // VK_F12 = 0x7B

    /// <summary>
    /// Short display name for a VK code. Only covers the keys we care about; unknown codes
    /// fall back to a hex representation so nothing crashes.
    /// </summary>
    private static string VirtualKeyName(uint vk) => vk switch
    {
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F), // F1..F24
        >= 0x30 and <= 0x39 => ((char)vk).ToString(), // 0..9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(), // A..Z
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x1B => "Esc",
        _    => $"VK_{vk:X2}",
    };
}
