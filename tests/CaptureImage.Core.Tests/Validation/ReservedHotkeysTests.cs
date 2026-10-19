// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Validation;
using Xunit;

namespace CaptureImage.Core.Tests.Validation;

public class ReservedHotkeysTests
{
    [Theory]
    [InlineData(HotkeyModifiers.Win, 0x4Cu)]                          // Win+L
    [InlineData(HotkeyModifiers.Win, 0x47u)]                          // Win+G
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x52u)]    // Win+Alt+R
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x53u)]  // Win+Shift+S
    [InlineData(HotkeyModifiers.None, 0x2Cu)]                         // PrtScn
    [InlineData(HotkeyModifiers.Alt, 0x09u)]                          // Alt+Tab
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x1Bu)] // Ctrl+Shift+Esc
    public void IsReserved_ShellShortcuts(HotkeyModifiers modifiers, uint virtualKey)
    {
        ReservedHotkeys.IsReserved(new HotkeyBinding(modifiers, virtualKey)).Should().BeTrue();
    }

    [Theory]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x47u)]  // Win+Shift+G (only Win+G is)
    [InlineData(HotkeyModifiers.Win, 0x53u)]                          // Win+S without Shift
    [InlineData(HotkeyModifiers.Alt, 0x52u)]                          // Alt+R without Win
    [InlineData(HotkeyModifiers.Control, 0x2Cu)]                      // Ctrl+PrtScn
    [InlineData(HotkeyModifiers.None, 0x73u)]                         // bare F4 (only Alt+F4 is)
    public void IsReserved_RequiresTheExactModifiers(HotkeyModifiers modifiers, uint virtualKey)
    {
        ReservedHotkeys.IsReserved(new HotkeyBinding(modifiers, virtualKey)).Should().BeFalse();
    }

    [Fact]
    public void NoDefaultBinding_IsReserved()
    {
        foreach (var mode in Enum.GetValues<CaptureMode>())
        {
            ReservedHotkeys.IsReserved(HotkeyBinding.DefaultFor(mode)).Should().BeFalse();
        }

        ReservedHotkeys.IsReserved(HotkeyBinding.Unbound).Should().BeFalse();
    }
}
