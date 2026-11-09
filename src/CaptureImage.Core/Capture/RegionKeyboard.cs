// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Capture;

/// <summary>What a key does while the region overlay is up.</summary>
public enum RegionKeyAction
{
    /// <summary>Close the overlay without capturing (Esc).</summary>
    Cancel,

    /// <summary>Capture the selection, or the whole monitor when nothing is selected (Enter).</summary>
    Confirm,

    /// <summary>Move the selection by <see cref="RegionKeyCommand.Dx"/> / <see cref="RegionKeyCommand.Dy"/>.</summary>
    Move,

    /// <summary>Grow or shrink the selection from its bottom-right corner.</summary>
    Resize,

    /// <summary>Continue on the next monitor (Tab).</summary>
    NextMonitor,

    /// <summary>Continue on the previous monitor (Shift+Tab).</summary>
    PreviousMonitor,
}

/// <summary>A key press translated for the region overlay; the offsets are in physical pixels.</summary>
public readonly record struct RegionKeyCommand(RegionKeyAction Action, int Dx = 0, int Dy = 0);

/// <summary>
/// Keyboard control of the region overlay, in physical pixels relative to the monitor: the
/// arrow keys move the selection, Shift+arrows resize it, Ctrl turns either into a one-pixel
/// step, Tab / Shift+Tab continue on the next / previous monitor, Enter captures and Esc
/// cancels. The keys are registered only while the overlay is up, so they work even when a
/// full-screen game keeps the keyboard focus.
/// </summary>
public static class RegionKeyboard
{
    /// <summary>Smallest selection, in physical pixels, that is captured instead of the whole monitor.</summary>
    public const int MinimumSize = 4;

    /// <summary>Length of a normal step in device-independent pixels (scaled by the monitor's DPI).</summary>
    public const int StepDips = 10;

    private const uint VkTab = 0x09;
    private const uint VkEnter = 0x0D;
    private const uint VkEscape = 0x1B;
    private const uint VkLeft = 0x25;
    private const uint VkUp = 0x26;
    private const uint VkRight = 0x27;
    private const uint VkDown = 0x28;

    /// <summary>Keys that act once per press: Esc, Enter, Tab and Shift+Tab.</summary>
    public static IReadOnlyList<HotkeyBinding> SingleKeys { get; } =
    [
        new(HotkeyModifiers.None, VkEscape),
        new(HotkeyModifiers.None, VkEnter),
        new(HotkeyModifiers.None, VkTab),
        new(HotkeyModifiers.Shift, VkTab),
    ];

    /// <summary>The arrow keys alone and with Shift, Ctrl and Ctrl+Shift; holding one repeats it.</summary>
    public static IReadOnlyList<HotkeyBinding> RepeatingKeys { get; } =
    [
        .. new[] { HotkeyModifiers.None, HotkeyModifiers.Shift, HotkeyModifiers.Control, HotkeyModifiers.Control | HotkeyModifiers.Shift }
            .SelectMany(modifiers => new[] { VkLeft, VkUp, VkRight, VkDown }.Select(key => new HotkeyBinding(modifiers, key))),
    ];

    /// <summary>A normal step in physical pixels on a monitor with the given scale (1.0 = 100 %).</summary>
    public static int StepFor(double scale) => Math.Max(1, (int)Math.Round(StepDips * scale, MidpointRounding.AwayFromZero));

    /// <summary>What <paramref name="key"/> does, or <see langword="null"/> when the overlay ignores it.</summary>
    /// <param name="key">The key and the modifiers held with it.</param>
    /// <param name="step">Length of a normal step in physical pixels (see <see cref="StepFor"/>).</param>
    public static RegionKeyCommand? Map(HotkeyBinding key, int step)
    {
        var modifiers = key.Modifiers;
        switch (key.VirtualKey)
        {
            case VkEscape when modifiers == HotkeyModifiers.None:
                return new RegionKeyCommand(RegionKeyAction.Cancel);
            case VkEnter when modifiers == HotkeyModifiers.None:
                return new RegionKeyCommand(RegionKeyAction.Confirm);
            case VkTab when modifiers == HotkeyModifiers.None:
                return new RegionKeyCommand(RegionKeyAction.NextMonitor);
            case VkTab when modifiers == HotkeyModifiers.Shift:
                return new RegionKeyCommand(RegionKeyAction.PreviousMonitor);
        }

        if ((modifiers & ~(HotkeyModifiers.Control | HotkeyModifiers.Shift)) != 0)
        {
            return null;
        }

        (int X, int Y)? direction = key.VirtualKey switch
        {
            VkLeft => (-1, 0),
            VkRight => (1, 0),
            VkUp => (0, -1),
            VkDown => (0, 1),
            _ => null,
        };
        if (direction is not { } d)
        {
            return null;
        }

        var distance = modifiers.HasFlag(HotkeyModifiers.Control) ? 1 : Math.Max(1, step);
        var action = modifiers.HasFlag(HotkeyModifiers.Shift) ? RegionKeyAction.Resize : RegionKeyAction.Move;
        return new RegionKeyCommand(action, d.X * distance, d.Y * distance);
    }

    /// <summary>The selection a first arrow key creates: half the monitor, centred.</summary>
    public static PixelRect Initial(int width, int height)
    {
        var w = Math.Max(1, width / 2);
        var h = Math.Max(1, height / 2);
        return new PixelRect((width - w) / 2, (height - h) / 2, w, h);
    }

    /// <summary>
    /// The selection after <paramref name="command"/> on a monitor of the given size. Without a
    /// selection a move or resize only creates the <see cref="Initial"/> one. Moves stop at the
    /// monitor's edges; resizes keep the top-left corner and stay between
    /// <see cref="MinimumSize"/> and the monitor's edges.
    /// </summary>
    public static PixelRect? Apply(PixelRect? current, RegionKeyCommand command, int width, int height)
    {
        if (command.Action is not (RegionKeyAction.Move or RegionKeyAction.Resize))
        {
            return current;
        }

        if (current is not { IsEmpty: false } rect)
        {
            return Initial(width, height);
        }

        return command.Action == RegionKeyAction.Move
            ? rect with
            {
                X = Math.Clamp(rect.X + command.Dx, 0, Math.Max(0, width - rect.Width)),
                Y = Math.Clamp(rect.Y + command.Dy, 0, Math.Max(0, height - rect.Height)),
            }
            : rect with
            {
                Width = ClampSize(rect.Width + command.Dx, width - rect.X),
                Height = ClampSize(rect.Height + command.Dy, height - rect.Y),
            };
    }

    /// <summary>
    /// Index of the monitor after (or before) <paramref name="current"/>, ordering the monitors
    /// left to right and then top to bottom and wrapping around at the ends.
    /// </summary>
    public static int Cycle(IReadOnlyList<PixelRect> monitors, int current, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            return current;
        }

        var order = Enumerable.Range(0, monitors.Count)
            .OrderBy(i => monitors[i].X)
            .ThenBy(i => monitors[i].Y)
            .ToList();
        var position = order.IndexOf(current);
        if (position < 0)
        {
            return order[0];
        }

        return order[(position + (backwards ? order.Count - 1 : 1)) % order.Count];
    }

    private static int ClampSize(int value, int available) => Math.Clamp(value, Math.Min(MinimumSize, available), available);
}
