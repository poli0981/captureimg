using CaptureImage.Core.Models;

namespace CaptureImage.Core.Validation;

/// <summary>
/// Catalogue of hotkey combinations reserved by the Windows shell. Binding one of these
/// still registers on our side, but Windows consumes the key press before our hook sees
/// it, so the <see cref="Core.Abstractions.IHotkeyService"/> would silently never fire.
/// The Settings UI uses this to warn the user before they persist an unreachable combo.
/// </summary>
public static class ReservedHotkeys
{
    private static readonly HotkeyBinding[] ReservedList =
    {
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
        // Window snapping
        new(HotkeyModifiers.Win, 0x25),                                 // Win+Left
        new(HotkeyModifiers.Win, 0x26),                                 // Win+Up
        new(HotkeyModifiers.Win, 0x27),                                 // Win+Right
        new(HotkeyModifiers.Win, 0x28),                                 // Win+Down
        // Task Manager / Security
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x1B),     // Ctrl+Shift+Esc
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x2E),       // Ctrl+Alt+Del (also intercepted by kernel)
        // Window management owned by Alt
        new(HotkeyModifiers.Alt, 0x09),                                 // Alt+Tab
        new(HotkeyModifiers.Alt, 0x73),                                 // Alt+F4
    };

    public static IReadOnlyList<HotkeyBinding> All => ReservedList;

    public static bool IsReserved(HotkeyBinding binding) =>
        ReservedList.Any(r => r == binding);
}
