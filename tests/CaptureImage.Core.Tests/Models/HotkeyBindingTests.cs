// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class HotkeyBindingTests
{
    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    // ---- Validate: accepted -------------------------------------------------------------

    [Fact]
    public void Default_IsCtrlShiftF12_AndValid()
    {
        HotkeyBinding.Default.Should().Be(new HotkeyBinding(CtrlShift, 0x7B));
        HotkeyBinding.Default.Validate().Should().Be(HotkeyValidationResult.Ok);
        HotkeyBinding.Default.IsValid().Should().BeTrue();
    }

    [Theory]
    [InlineData(CtrlShift, 0x7Bu)]                                   // Ctrl+Shift+F12
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78u)] // Ctrl+Alt+F9
    [InlineData(HotkeyModifiers.Shift, 0x41u)]                       // Shift+A
    [InlineData(HotkeyModifiers.Win, 0x74u)]                         // Win+F5
    [InlineData(HotkeyModifiers.Control, 0x2Cu)]                     // Ctrl+PrtScn
    [InlineData(HotkeyModifiers.Shift, 0x2Cu)]                       // Shift+PrtScn
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x47u)] // Win+Shift+G
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Control, 0x53u)] // Win+Ctrl+S
    [InlineData(HotkeyModifiers.Alt, 0x74u)]                         // Alt+F5 (only Alt+F4 is reserved)
    public void Validate_ModifierCombinations_AreOk(HotkeyModifiers modifiers, uint virtualKey)
    {
        new HotkeyBinding(modifiers, virtualKey).Validate().Should().Be(HotkeyValidationResult.Ok);
    }

    [Theory]
    [InlineData(0x70u)] // F1
    [InlineData(0x73u)] // F4
    [InlineData(0x7Bu)] // F12
    [InlineData(0x7Cu)] // F13
    [InlineData(0x87u)] // F24
    public void Validate_BareFunctionKeys_AreAllowed(uint virtualKey)
    {
        new HotkeyBinding(HotkeyModifiers.None, virtualKey).Validate().Should().Be(HotkeyValidationResult.Ok);
    }

    // ---- Validate: rejected -------------------------------------------------------------

    [Theory]
    [InlineData(HotkeyModifiers.None)]
    [InlineData(HotkeyModifiers.Control)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win)]
    public void Validate_WithoutPrimaryKey_IsNoPrimaryKey(HotkeyModifiers modifiers)
    {
        var binding = new HotkeyBinding(modifiers, 0);

        binding.Validate().Should().Be(HotkeyValidationResult.NoPrimaryKey);
        binding.IsValid().Should().BeFalse();
    }

    [Theory]
    [InlineData(0x10u)] // VK_SHIFT
    [InlineData(0x11u)] // VK_CONTROL
    [InlineData(0x12u)] // VK_MENU (Alt)
    [InlineData(0x5Bu)] // VK_LWIN
    [InlineData(0x5Cu)] // VK_RWIN
    [InlineData(0xA0u)] // VK_LSHIFT
    [InlineData(0xA1u)] // VK_RSHIFT
    [InlineData(0xA2u)] // VK_LCONTROL
    [InlineData(0xA3u)] // VK_RCONTROL
    [InlineData(0xA4u)] // VK_LMENU
    [InlineData(0xA5u)] // VK_RMENU
    public void Validate_ModifierAsPrimaryKey_IsModifierOnlyKey(uint virtualKey)
    {
        // Even with modifiers held, a modifier VK as the "primary" key means the recorder
        // has not seen a real key yet.
        new HotkeyBinding(HotkeyModifiers.Control, virtualKey).Validate().Should().Be(HotkeyValidationResult.ModifierOnlyKey);
        new HotkeyBinding(HotkeyModifiers.None, virtualKey).Validate().Should().Be(HotkeyValidationResult.ModifierOnlyKey);
    }

    [Theory]
    [InlineData(0x41u)] // A
    [InlineData(0x5Au)] // Z
    [InlineData(0x30u)] // 0
    [InlineData(0x39u)] // 9
    [InlineData(0x20u)] // Space
    [InlineData(0x0Du)] // Enter
    [InlineData(0x60u)] // Num0
    public void Validate_PlainKeyWithoutModifier_RequiresModifier(uint virtualKey)
    {
        new HotkeyBinding(HotkeyModifiers.None, virtualKey).Validate().Should().Be(HotkeyValidationResult.RequiresModifier);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, 0x2Cu)]                         // PrtScn — Snipping Tool
    [InlineData(HotkeyModifiers.Alt, 0x2Cu)]                          // Alt+PrtScn
    [InlineData(HotkeyModifiers.Win, 0x2Cu)]                          // Win+PrtScn
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x2Cu)]    // Win+Alt+PrtScn
    [InlineData(HotkeyModifiers.Win, 0x47u)]                          // Win+G — Game Bar
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x53u)]  // Win+Shift+S
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x52u)]    // Win+Alt+R
    [InlineData(HotkeyModifiers.Win, 0x4Cu)]                          // Win+L
    [InlineData(HotkeyModifiers.Win, 0x44u)]                          // Win+D
    [InlineData(HotkeyModifiers.Win, 0x09u)]                          // Win+Tab
    [InlineData(HotkeyModifiers.Win, 0x25u)]                          // Win+Left
    [InlineData(HotkeyModifiers.Alt, 0x09u)]                          // Alt+Tab
    [InlineData(HotkeyModifiers.Alt, 0x73u)]                          // Alt+F4
    [InlineData(CtrlShift, 0x1Bu)]                                    // Ctrl+Shift+Esc
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x2Eu)] // Ctrl+Alt+Del
    public void Validate_CombinationOwnedByWindows_IsReservedByWindows(HotkeyModifiers modifiers, uint virtualKey)
    {
        var binding = new HotkeyBinding(modifiers, virtualKey);

        binding.Validate().Should().Be(HotkeyValidationResult.ReservedByWindows);
        binding.IsValid().Should().BeFalse();
    }

    // ---- Unbound / defaults ---------------------------------------------------------------

    [Fact]
    public void Unbound_IsTheDefaultValue_AndRendersEmpty()
    {
        HotkeyBinding.Unbound.Should().Be(default(HotkeyBinding));
        HotkeyBinding.Unbound.IsUnbound.Should().BeTrue();
        HotkeyBinding.Unbound.ToString().Should().BeEmpty();
        HotkeyBinding.Unbound.IsValid().Should().BeFalse("an unbound mode is allowed, but it is not a usable hotkey");
        HotkeyBinding.Default.IsUnbound.Should().BeFalse();
    }

    [Theory]
    [InlineData(CaptureMode.Window, "Ctrl+Shift+F12")]
    [InlineData(CaptureMode.Region, "Ctrl+Shift+F11")]
    [InlineData(CaptureMode.Monitor, "Ctrl+Shift+F10")]
    public void DefaultFor_EachMode(CaptureMode mode, string expected)
    {
        var binding = HotkeyBinding.DefaultFor(mode);

        binding.ToString().Should().Be(expected);
        binding.IsValid().Should().BeTrue();
    }

    [Fact]
    public void DefaultFor_GivesThreeDistinctValidBindings()
    {
        var defaults = Enum.GetValues<CaptureMode>().Select(HotkeyBinding.DefaultFor).ToList();

        defaults.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        defaults.Should().OnlyContain(binding => binding.Validate() == HotkeyValidationResult.Ok);
        HotkeyBinding.DefaultFor(CaptureMode.Window).Should().Be(HotkeyBinding.Default);
    }

    [Fact]
    public void DefaultFor_UnknownMode_IsUnbound()
    {
        HotkeyBinding.DefaultFor((CaptureMode)42).Should().Be(HotkeyBinding.Unbound);
    }

    // ---- ToString ----------------------------------------------------------------------------

    [Fact]
    public void ToString_ListsModifiersInCanonicalOrder()
    {
        var all = HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control;

        new HotkeyBinding(all, 0x41).ToString().Should().Be("Ctrl+Alt+Shift+Win+A");
        new HotkeyBinding(HotkeyModifiers.Shift | HotkeyModifiers.Alt, 0x7B).ToString().Should().Be("Alt+Shift+F12");
        new HotkeyBinding(HotkeyModifiers.None, 0x70).ToString().Should().Be("F1");
    }

    [Theory]
    [InlineData(0x70u, "F1")]
    [InlineData(0x7Bu, "F12")]
    [InlineData(0x87u, "F24")]
    [InlineData(0x30u, "0")]
    [InlineData(0x39u, "9")]
    [InlineData(0x41u, "A")]
    [InlineData(0x5Au, "Z")]
    [InlineData(0x60u, "Num0")]
    [InlineData(0x69u, "Num9")]
    [InlineData(0x20u, "Space")]
    [InlineData(0x0Du, "Enter")]
    [InlineData(0x09u, "Tab")]
    [InlineData(0x1Bu, "Esc")]
    [InlineData(0x2Cu, "PrtScn")]
    [InlineData(0x13u, "Pause")]
    [InlineData(0x91u, "ScrollLock")]
    [InlineData(0x2Du, "Insert")]
    [InlineData(0x2Eu, "Delete")]
    [InlineData(0x24u, "Home")]
    [InlineData(0x23u, "End")]
    [InlineData(0x21u, "PgUp")]
    [InlineData(0x22u, "PgDn")]
    [InlineData(0x25u, "Left")]
    [InlineData(0x26u, "Up")]
    [InlineData(0x27u, "Right")]
    [InlineData(0x28u, "Down")]
    [InlineData(0xC0u, "`")]
    [InlineData(0xBDu, "-")]
    [InlineData(0xBBu, "=")]
    [InlineData(0xDBu, "[")]
    [InlineData(0xDDu, "]")]
    [InlineData(0xDCu, "\\")]
    [InlineData(0xBAu, ";")]
    [InlineData(0xDEu, "'")]
    [InlineData(0xBCu, ",")]
    [InlineData(0xBEu, ".")]
    [InlineData(0xBFu, "/")]
    [InlineData(0x07u, "VK_07")]
    [InlineData(0xFFu, "VK_FF")]
    [InlineData(0x1A2u, "VK_1A2")]
    public void ToString_NamesTheKey(uint virtualKey, string keyName)
    {
        new HotkeyBinding(HotkeyModifiers.Control, virtualKey).ToString().Should().Be("Ctrl+" + keyName);
    }
}
