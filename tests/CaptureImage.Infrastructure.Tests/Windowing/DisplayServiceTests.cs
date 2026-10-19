// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Infrastructure.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Windowing;

/// <summary>
/// <see cref="DisplayService"/> on the real desktop: whatever monitors are attached, the
/// description must be internally consistent (one primary, work area inside the bounds, sane
/// DPI and HDR values).
/// </summary>
public sealed class DisplayServiceTests : IDisposable
{
    private readonly DisplayService _displays = new(NullLogger<DisplayService>.Instance);

    public void Dispose() => _displays.Dispose();

    [Fact]
    public void GetMonitors_DescribesEveryAttachedMonitor()
    {
        var monitors = _displays.GetMonitors();

        monitors.Should().NotBeEmpty();
        monitors.Should().ContainSingle(m => m.IsPrimary);
        monitors.Select(m => m.Handle).Should().OnlyHaveUniqueItems();
        foreach (var monitor in monitors)
        {
            monitor.Handle.Should().NotBe(0);
            monitor.DeviceName.Should().NotBeNullOrWhiteSpace();
            monitor.Bounds.IsEmpty.Should().BeFalse();
            monitor.WorkArea.IsEmpty.Should().BeFalse();
            monitor.WorkArea.Intersect(monitor.Bounds).Should().Be(monitor.WorkArea, "the work area lies inside the monitor");
            monitor.Dpi.Should().BeGreaterThanOrEqualTo(96);
            monitor.Scale.Should().BeApproximately(monitor.Dpi / 96.0, 1e-9);
            monitor.Hdr.Should().NotBeNull();
            monitor.Hdr.SdrWhiteNits.Should().BeGreaterThanOrEqualTo(HdrInfo.ScRgbReferenceWhiteNits);
            monitor.Hdr.MaxLuminanceNits.Should().BeGreaterThanOrEqualTo(HdrInfo.ScRgbReferenceWhiteNits);
            if (!monitor.Hdr.IsHdrActive)
            {
                monitor.Hdr.Should().Be(HdrInfo.Sdr);
            }
        }
    }

    [Fact]
    public void GetMonitors_PrimaryMonitorStartsAtTheVirtualDesktopOrigin()
    {
        var primary = _displays.GetMonitors().Single(m => m.IsPrimary);

        (primary.Bounds.X, primary.Bounds.Y).Should().Be((0, 0));
    }

    [Fact]
    public void GetMonitorAtCursor_IsOneOfTheMonitors()
    {
        var monitor = _displays.GetMonitorAtCursor();

        if (monitor is null)
        {
            Assert.Skip("The cursor position is not available in this session (no interactive desktop).");
        }
        _displays.GetMonitors().Select(m => m.Handle).Should().Contain(monitor.Handle);
    }

    [Fact]
    public void GetMonitorForWindow_UnknownWindow_FallsBackToTheNearestMonitor()
    {
        // MONITOR_DEFAULTTONEAREST: even a handle that is not a window yields a monitor.
        var monitor = _displays.GetMonitorForWindow(0);

        monitor.Should().NotBeNull();
        _displays.GetMonitors().Select(m => m.Handle).Should().Contain(monitor.Handle);
    }

    [Fact]
    public void GetMonitors_WorksRepeatedly_AndAfterDispose()
    {
        var first = _displays.GetMonitors();
        var second = _displays.GetMonitors();
        _displays.Dispose();
        var afterDispose = _displays.GetMonitors();

        second.Should().BeEquivalentTo(first);
        afterDispose.Select(m => m.Handle).Should().BeEquivalentTo(first.Select(m => m.Handle));
    }
}
