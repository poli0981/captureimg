using CaptureImage.Core.Models;
using CaptureImage.Core.Validation;
using FluentAssertions;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class HotkeyBindingValidationTests
{
    // -- acceptance cases -----------------------------------------------------

    [Fact]
    public void Default_IsValid()
    {
        HotkeyBinding.Default.Validate().Should().Be(HotkeyValidationResult.Ok);
        HotkeyBinding.Default.IsValid().Should().BeTrue();
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B)] // Ctrl+Shift+F12
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78)]   // Ctrl+Alt+F9
    [InlineData(HotkeyModifiers.Shift, 0x41)]                           // Shift+A
    [InlineData(HotkeyModifiers.Win, 0x74)]                             // Win+F5
    public void Validate_ValidModifierCombos_ReturnsOk(HotkeyModifiers mods, uint vk)
    {
        new HotkeyBinding(mods, vk).Validate()
            .Should().Be(HotkeyValidationResult.Ok);
    }

    [Theory]
    [InlineData(0x70)] // F1
    [InlineData(0x7B)] // F12
    [InlineData(0x87)] // F24
    [InlineData(0x2C)] // PrintScreen
    public void Validate_FunctionKey_AllowedWithoutModifier(uint vk)
    {
        new HotkeyBinding(HotkeyModifiers.None, vk).Validate()
            .Should().Be(HotkeyValidationResult.Ok);
    }

    // -- rejection cases ------------------------------------------------------

    [Fact]
    public void Validate_NoPrimaryKey()
    {
        new HotkeyBinding(HotkeyModifiers.Control, 0).Validate()
            .Should().Be(HotkeyValidationResult.NoPrimaryKey);
    }

    [Theory]
    [InlineData(0x10)] // VK_SHIFT
    [InlineData(0x11)] // VK_CONTROL
    [InlineData(0x12)] // VK_MENU (Alt)
    [InlineData(0x5B)] // VK_LWIN
    [InlineData(0x5C)] // VK_RWIN
    [InlineData(0xA0)] // VK_LSHIFT
    [InlineData(0xA2)] // VK_LCONTROL
    [InlineData(0xA4)] // VK_LMENU
    public void Validate_ModifierAsPrimaryKey_IsRejected(uint vk)
    {
        // Even with modifiers pressed, a modifier VK as the "primary" is invalid —
        // the caller should have waited for a real key.
        new HotkeyBinding(HotkeyModifiers.Control, vk).Validate()
            .Should().Be(HotkeyValidationResult.ModifierOnlyKey);
    }

    [Theory]
    [InlineData(0x41)] // 'A'
    [InlineData(0x30)] // '0'
    [InlineData(0x20)] // Space
    public void Validate_PlainKeyWithoutModifier_RequiresModifier(uint vk)
    {
        new HotkeyBinding(HotkeyModifiers.None, vk).Validate()
            .Should().Be(HotkeyValidationResult.RequiresModifier);
    }

    // -- Windows-reserved combos ---------------------------------------------

    [Theory]
    [InlineData(HotkeyModifiers.Win, 0x4C)]                                 // Win+L
    [InlineData(HotkeyModifiers.Win, 0x44)]                                 // Win+D
    [InlineData(HotkeyModifiers.Alt, 0x09)]                                 // Alt+Tab
    [InlineData(HotkeyModifiers.Alt, 0x73)]                                 // Alt+F4
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x1B)]     // Ctrl+Shift+Esc
    public void Validate_ReservedByWindows_IsFlagged(HotkeyModifiers mods, uint vk)
    {
        new HotkeyBinding(mods, vk).Validate()
            .Should().Be(HotkeyValidationResult.ReservedByWindows);
    }

    [Fact]
    public void ReservedHotkeys_All_IncludesCoreShellCombos()
    {
        ReservedHotkeys.All.Should().NotBeEmpty();
        ReservedHotkeys.IsReserved(new HotkeyBinding(HotkeyModifiers.Win, 0x4C)).Should().BeTrue();
        ReservedHotkeys.IsReserved(new HotkeyBinding(HotkeyModifiers.Alt, 0x09)).Should().BeTrue();
        // Something conspicuously NOT on the reserved list:
        ReservedHotkeys.IsReserved(HotkeyBinding.Default).Should().BeFalse();
    }
}
