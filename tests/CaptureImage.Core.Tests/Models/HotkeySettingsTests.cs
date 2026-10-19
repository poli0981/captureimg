// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class HotkeySettingsTests
{
    private static readonly HotkeyBinding AltF9 = new(HotkeyModifiers.Alt, 0x78);

    [Fact]
    public void Defaults_AreTheDefaultBindingOfEachMode()
    {
        var hotkeys = new HotkeySettings();

        foreach (var mode in Enum.GetValues<CaptureMode>())
        {
            hotkeys.Get(mode).Should().Be(HotkeyBinding.DefaultFor(mode));
        }

        hotkeys.Window.Should().Be(HotkeyBinding.Default);
        hotkeys.ExtensionData.Should().BeNull();
    }

    [Theory]
    [InlineData(CaptureMode.Window)]
    [InlineData(CaptureMode.Region)]
    [InlineData(CaptureMode.Monitor)]
    public void With_ThenGet_RoundTrips_AndLeavesTheOtherModesAlone(CaptureMode mode)
    {
        var original = new HotkeySettings();

        var updated = original.With(mode, AltF9);

        updated.Get(mode).Should().Be(AltF9);
        foreach (var other in Enum.GetValues<CaptureMode>().Where(m => m != mode))
        {
            updated.Get(other).Should().Be(original.Get(other));
        }

        original.Get(mode).Should().Be(HotkeyBinding.DefaultFor(mode), "With returns a copy");
        updated.Should().NotBeSameAs(original);
    }

    [Fact]
    public void With_MapsEachModeToItsProperty()
    {
        var hotkeys = new HotkeySettings()
            .With(CaptureMode.Window, new HotkeyBinding(HotkeyModifiers.Alt, 0x70))
            .With(CaptureMode.Region, new HotkeyBinding(HotkeyModifiers.Alt, 0x71))
            .With(CaptureMode.Monitor, new HotkeyBinding(HotkeyModifiers.Alt, 0x72));

        hotkeys.Window.VirtualKey.Should().Be(0x70);
        hotkeys.Region.VirtualKey.Should().Be(0x71);
        hotkeys.Monitor.VirtualKey.Should().Be(0x72);
    }

    [Fact]
    public void UnknownMode_IsUnbound_AndWithIgnoresIt()
    {
        var hotkeys = new HotkeySettings();

        hotkeys.Get((CaptureMode)42).Should().Be(HotkeyBinding.Unbound);
        hotkeys.With((CaptureMode)42, AltF9).Should().BeSameAs(hotkeys);
    }

    [Fact]
    public void Equality_ComparesBindings()
    {
        new HotkeySettings().Should().Be(new HotkeySettings());
        new HotkeySettings().With(CaptureMode.Region, AltF9).Should().NotBe(new HotkeySettings());
    }
}
