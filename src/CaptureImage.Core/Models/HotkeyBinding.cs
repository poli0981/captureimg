using CaptureImage.Core.Validation;

namespace CaptureImage.Core.Models;

/// <summary>
/// Outcome of <see cref="HotkeyBinding.Validate"/>. UI inspects this to render the right
/// error or warning string; persistence layer only persists bindings whose result is
/// <see cref="Ok"/>.
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
    /// Classify this binding for use in the UI rebinder. See <see cref="HotkeyValidationResult"/>
    /// for possible outcomes. Pure function — does not touch Windows state.
    /// </summary>
    public HotkeyValidationResult Validate()
    {
        if (VirtualKey == 0)
            return HotkeyValidationResult.NoPrimaryKey;

        if (IsModifierVk(VirtualKey))
            return HotkeyValidationResult.ModifierOnlyKey;

        if (Modifiers == HotkeyModifiers.None && !IsFunctionOrSpecial(VirtualKey))
            return HotkeyValidationResult.RequiresModifier;

        if (ReservedHotkeys.IsReserved(this))
            return HotkeyValidationResult.ReservedByWindows;

        return HotkeyValidationResult.Ok;
    }

    /// <summary>True when <see cref="Validate"/> returns <see cref="HotkeyValidationResult.Ok"/>.</summary>
    public bool IsValid() => Validate() == HotkeyValidationResult.Ok;

    private static bool IsModifierVk(uint vk) => vk is
        0x10 or 0x11 or 0x12 or            // VK_SHIFT, VK_CONTROL, VK_MENU (Alt)
        0x5B or 0x5C or                    // VK_LWIN, VK_RWIN
        0xA0 or 0xA1 or                    // VK_LSHIFT, VK_RSHIFT
        0xA2 or 0xA3 or                    // VK_LCONTROL, VK_RCONTROL
        0xA4 or 0xA5;                      // VK_LMENU,   VK_RMENU

    private static bool IsFunctionOrSpecial(uint vk) =>
        (vk >= 0x70 && vk <= 0x87) ||      // F1..F24
        vk == 0x2C;                        // VK_SNAPSHOT (PrintScreen)

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
