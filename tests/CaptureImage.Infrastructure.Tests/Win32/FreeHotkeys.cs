// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace CaptureImage.Infrastructure.Tests.Win32;

/// <summary>
/// Picks Ctrl+Alt+Shift+F19…F24 combinations that no other program holds right now, probing
/// with raw <c>RegisterHotKey</c> (released immediately) so that the code under test is not
/// used to decide whether it works.
/// </summary>
internal static class FreeHotkeys
{
    private const HotkeyModifiers Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
    private const int ProbeId = 0x7F00;

    private static readonly uint[] Candidates = [0x87, 0x86, 0x85, 0x84, 0x83, 0x82]; // VK_F24 … VK_F19

    public static IReadOnlyList<HotkeyBinding> Find(int count)
    {
        var free = new List<HotkeyBinding>(count);
        foreach (var vk in Candidates)
        {
            if (PInvoke.RegisterHotKey(HWND.Null, ProbeId, (HOT_KEY_MODIFIERS)Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT, vk))
            {
                PInvoke.UnregisterHotKey(HWND.Null, ProbeId);
                free.Add(new HotkeyBinding(Modifiers, vk));
                if (free.Count == count)
                {
                    break;
                }
            }
        }
        return free;
    }

    /// <summary>True if the combination can be registered right now (it is then released again).</summary>
    public static bool IsFree(HotkeyBinding binding)
    {
        if (!PInvoke.RegisterHotKey(HWND.Null, ProbeId, (HOT_KEY_MODIFIERS)binding.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT, binding.VirtualKey))
        {
            return false;
        }
        PInvoke.UnregisterHotKey(HWND.Null, ProbeId);
        return true;
    }
}
