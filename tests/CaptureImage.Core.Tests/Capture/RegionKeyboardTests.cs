// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Capture;

public class RegionKeyboardTests
{
    private const uint Tab = 0x09;
    private const uint Enter = 0x0D;
    private const uint Escape = 0x1B;
    private const uint Left = 0x25;
    private const uint Up = 0x26;
    private const uint Right = 0x27;
    private const uint Down = 0x28;

    private static readonly HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    [Fact]
    public void SingleKeys_AreEscEnterTabAndShiftTab()
    {
        RegionKeyboard.SingleKeys.Should().Equal(
            new HotkeyBinding(HotkeyModifiers.None, Escape),
            new HotkeyBinding(HotkeyModifiers.None, Enter),
            new HotkeyBinding(HotkeyModifiers.None, Tab),
            new HotkeyBinding(HotkeyModifiers.Shift, Tab));
    }

    [Fact]
    public void RepeatingKeys_AreTheArrowsWithEveryModifierCombination()
    {
        RegionKeyboard.RepeatingKeys.Should().HaveCount(16).And.OnlyHaveUniqueItems();
        RegionKeyboard.RepeatingKeys.Select(k => k.VirtualKey).Distinct().Should().BeEquivalentTo(new[] { Left, Up, Right, Down });
        RegionKeyboard.RepeatingKeys.Select(k => k.Modifiers).Distinct().Should().BeEquivalentTo(
            new[] { HotkeyModifiers.None, HotkeyModifiers.Shift, HotkeyModifiers.Control, CtrlShift });
    }

    [Fact]
    public void EveryRegisteredKey_MapsToACommand()
    {
        RegionKeyboard.SingleKeys.Concat(RegionKeyboard.RepeatingKeys)
            .Should().OnlyContain(key => RegionKeyboard.Map(key, 10) != null);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, Escape, RegionKeyAction.Cancel)]
    [InlineData(HotkeyModifiers.None, Enter, RegionKeyAction.Confirm)]
    [InlineData(HotkeyModifiers.None, Tab, RegionKeyAction.NextMonitor)]
    [InlineData(HotkeyModifiers.Shift, Tab, RegionKeyAction.PreviousMonitor)]
    public void Map_SingleKeys(HotkeyModifiers modifiers, uint key, RegionKeyAction expected)
    {
        RegionKeyboard.Map(new HotkeyBinding(modifiers, key), 10).Should().Be(new RegionKeyCommand(expected));
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, Left, RegionKeyAction.Move, -10, 0)]
    [InlineData(HotkeyModifiers.None, Right, RegionKeyAction.Move, 10, 0)]
    [InlineData(HotkeyModifiers.None, Up, RegionKeyAction.Move, 0, -10)]
    [InlineData(HotkeyModifiers.None, Down, RegionKeyAction.Move, 0, 10)]
    [InlineData(HotkeyModifiers.Control, Left, RegionKeyAction.Move, -1, 0)]
    [InlineData(HotkeyModifiers.Shift, Right, RegionKeyAction.Resize, 10, 0)]
    [InlineData(HotkeyModifiers.Shift, Up, RegionKeyAction.Resize, 0, -10)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, Down, RegionKeyAction.Resize, 0, 1)]
    public void Map_Arrows(HotkeyModifiers modifiers, uint key, RegionKeyAction action, int dx, int dy)
    {
        RegionKeyboard.Map(new HotkeyBinding(modifiers, key), 10).Should().Be(new RegionKeyCommand(action, dx, dy));
    }

    [Fact]
    public void Map_StepIsAtLeastOnePixel()
    {
        RegionKeyboard.Map(new HotkeyBinding(HotkeyModifiers.None, Right), 0).Should().Be(new RegionKeyCommand(RegionKeyAction.Move, 1, 0));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Alt, Left)]
    [InlineData(HotkeyModifiers.Win, Up)]
    [InlineData(HotkeyModifiers.Control, Tab)]
    [InlineData(HotkeyModifiers.Shift, Escape)]
    [InlineData(HotkeyModifiers.Control, Enter)]
    [InlineData(HotkeyModifiers.None, 0x41u)] // A
    public void Map_OtherKeys_AreIgnored(HotkeyModifiers modifiers, uint key)
    {
        RegionKeyboard.Map(new HotkeyBinding(modifiers, key), 10).Should().BeNull();
    }

    [Theory]
    [InlineData(1.0, 10)]
    [InlineData(1.5, 15)]
    [InlineData(1.25, 13)]
    [InlineData(0.01, 1)]
    public void StepFor_ScalesWithTheMonitor(double scale, int expected)
    {
        RegionKeyboard.StepFor(scale).Should().Be(expected);
    }

    [Fact]
    public void Initial_IsHalfTheMonitorCentred()
    {
        RegionKeyboard.Initial(1920, 1080).Should().Be(new PixelRect(480, 270, 960, 540));
        RegionKeyboard.Initial(1, 1).Should().Be(new PixelRect(0, 0, 1, 1));
    }

    [Theory]
    [InlineData(RegionKeyAction.Move)]
    [InlineData(RegionKeyAction.Resize)]
    public void Apply_WithoutASelection_CreatesTheInitialOne(RegionKeyAction action)
    {
        var command = new RegionKeyCommand(action, 10, 0);

        RegionKeyboard.Apply(null, command, 1920, 1080).Should().Be(new PixelRect(480, 270, 960, 540));
        RegionKeyboard.Apply(default(PixelRect), command, 1920, 1080).Should().Be(new PixelRect(480, 270, 960, 540));
    }

    [Theory]
    [InlineData(RegionKeyAction.Cancel)]
    [InlineData(RegionKeyAction.Confirm)]
    [InlineData(RegionKeyAction.NextMonitor)]
    [InlineData(RegionKeyAction.PreviousMonitor)]
    public void Apply_OtherCommands_KeepTheSelection(RegionKeyAction action)
    {
        var rect = new PixelRect(10, 20, 30, 40);

        RegionKeyboard.Apply(rect, new RegionKeyCommand(action), 100, 100).Should().Be(rect);
        RegionKeyboard.Apply(null, new RegionKeyCommand(action), 100, 100).Should().BeNull();
    }

    [Fact]
    public void Apply_Move_ShiftsTheSelection()
    {
        RegionKeyboard.Apply(new PixelRect(10, 20, 30, 40), new RegionKeyCommand(RegionKeyAction.Move, 5, -5), 100, 100)
            .Should().Be(new PixelRect(15, 15, 30, 40));
    }

    [Theory]
    [InlineData(-50, 0, 0, 20)]
    [InlineData(100, 0, 70, 20)]
    [InlineData(0, -50, 10, 0)]
    [InlineData(0, 100, 10, 60)]
    public void Apply_Move_StopsAtTheEdges(int dx, int dy, int x, int y)
    {
        RegionKeyboard.Apply(new PixelRect(10, 20, 30, 40), new RegionKeyCommand(RegionKeyAction.Move, dx, dy), 100, 100)
            .Should().Be(new PixelRect(x, y, 30, 40));
    }

    [Fact]
    public void Apply_Move_OfAFullWidthSelection_KeepsItInPlace()
    {
        RegionKeyboard.Apply(new PixelRect(0, 0, 100, 100), new RegionKeyCommand(RegionKeyAction.Move, 10, 10), 100, 100)
            .Should().Be(new PixelRect(0, 0, 100, 100));
    }

    [Fact]
    public void Apply_Resize_KeepsTheTopLeftCorner()
    {
        RegionKeyboard.Apply(new PixelRect(10, 20, 30, 40), new RegionKeyCommand(RegionKeyAction.Resize, 10, -10), 100, 100)
            .Should().Be(new PixelRect(10, 20, 40, 30));
    }

    [Theory]
    [InlineData(500, 500, 90, 80)]
    [InlineData(-500, -500, RegionKeyboard.MinimumSize, RegionKeyboard.MinimumSize)]
    public void Apply_Resize_StaysBetweenTheMinimumAndTheEdges(int dx, int dy, int width, int height)
    {
        RegionKeyboard.Apply(new PixelRect(10, 20, 30, 40), new RegionKeyCommand(RegionKeyAction.Resize, dx, dy), 100, 100)
            .Should().Be(new PixelRect(10, 20, width, height));
    }

    [Fact]
    public void Apply_Resize_NextToTheEdge_NeverExceedsTheMonitor()
    {
        RegionKeyboard.Apply(new PixelRect(98, 98, 2, 2), new RegionKeyCommand(RegionKeyAction.Resize, -1, 5), 100, 100)
            .Should().Be(new PixelRect(98, 98, 2, 2));
    }

    [Fact]
    public void Cycle_OrdersMonitorsLeftToRightThenTopToBottom()
    {
        // Enumeration order: centre, left, right-bottom, right-top.
        PixelRect[] monitors =
        [
            new(0, 0, 1920, 1080),
            new(-1920, 0, 1920, 1080),
            new(1920, 1080, 1920, 1080),
            new(1920, 0, 1920, 1080),
        ];

        RegionKeyboard.Cycle(monitors, 1, backwards: false).Should().Be(0);
        RegionKeyboard.Cycle(monitors, 0, backwards: false).Should().Be(3);
        RegionKeyboard.Cycle(monitors, 3, backwards: false).Should().Be(2);
        RegionKeyboard.Cycle(monitors, 2, backwards: false).Should().Be(1);
        RegionKeyboard.Cycle(monitors, 1, backwards: true).Should().Be(2);
        RegionKeyboard.Cycle(monitors, 0, backwards: true).Should().Be(1);
    }

    [Fact]
    public void Cycle_WithOneMonitor_StaysThere()
    {
        RegionKeyboard.Cycle([new PixelRect(0, 0, 10, 10)], 0, backwards: false).Should().Be(0);
        RegionKeyboard.Cycle([new PixelRect(0, 0, 10, 10)], 0, backwards: true).Should().Be(0);
    }

    [Fact]
    public void Cycle_UnknownIndex_StartsAtTheFirstMonitor()
    {
        RegionKeyboard.Cycle([new PixelRect(100, 0, 10, 10), new PixelRect(0, 0, 10, 10)], 7, backwards: false).Should().Be(1);
    }

    [Fact]
    public void Cycle_NoMonitors_ReturnsTheCurrentIndex()
    {
        RegionKeyboard.Cycle([], 2, backwards: false).Should().Be(2);
    }

    [Fact]
    public void Cycle_Null_Throws()
    {
        var act = () => RegionKeyboard.Cycle(null!, 0, backwards: false);

        act.Should().Throw<ArgumentNullException>();
    }
}
