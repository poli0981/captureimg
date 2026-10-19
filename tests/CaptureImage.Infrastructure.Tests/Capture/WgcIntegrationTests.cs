// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Buffers.Binary;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Capture;
using CaptureImage.Infrastructure.Tests.TestSupport;
using CaptureImage.Infrastructure.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Graphics.Capture;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Capture;

/// <summary>
/// <see cref="WgcCaptureEngine"/> and <see cref="D3D11DeviceManager"/> on the real GPU (or WARP):
/// a one-shot capture of the primary monitor must come back as an opaque frame of plausible
/// size. Skipped where Windows.Graphics.Capture is unavailable or no monitor is attached.
/// The captured pixels stay in memory; nothing is written to disk.
/// </summary>
[Trait("Category", "Windows")]
public sealed class WgcIntegrationTests : IDisposable
{
    private readonly D3D11DeviceManager _devices = new(NullLogger<D3D11DeviceManager>.Instance);
    private readonly WgcCaptureEngine _engine;
    private readonly DisplayService _displays = new(NullLogger<DisplayService>.Instance);

    public WgcIntegrationTests()
    {
        _engine = new WgcCaptureEngine(_devices, NullLogger<WgcCaptureEngine>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _engine.Dispose();
        _devices.Dispose();
        _displays.Dispose();
    }

    [Fact]
    public async Task CaptureAsync_PrimaryMonitor_ReturnsAnOpaqueBgraFrame()
    {
        var monitor = PrimaryMonitorOrSkip();

        var frame = await _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), CaptureOptions.Default, Ct);

        frame.Format.Should().Be(FramePixelFormat.Bgra8);
        frame.Width.Should().BePositive();
        frame.Height.Should().BePositive();
        frame.RowStride.Should().Be(frame.Width * 4);
        frame.Pixels.Should().HaveCount(frame.RowStride * frame.Height);
        TestFrames.IsOpaque(frame).Should().BeTrue("captured frames are forced opaque so saved images are never see-through");

        // Monitor bounds may be DPI-virtualized for this process, but the shape is the same and
        // the capture is never smaller than the logical size.
        frame.Width.Should().BeGreaterThanOrEqualTo(monitor.Bounds.Width);
        ((double)frame.Width / frame.Height).Should().BeApproximately((double)monitor.Bounds.Width / monitor.Bounds.Height, 0.02);
    }

    [Fact]
    public async Task CaptureAsync_PreferHdr_ReturnsAnOpaqueHalfFloatFrame_ThatToneMapsToEightBit()
    {
        var monitor = PrimaryMonitorOrSkip();

        var frame = await _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), new CaptureOptions(PreferHdr: true), Ct);

        // The coordinator only asks for HDR on HDR displays, but WGC delivers scRGB for any output.
        frame.Format.Should().Be(FramePixelFormat.RgbaHalf);
        frame.RowStride.Should().Be(frame.Width * 8);
        for (var i = 6; i < frame.Pixels.Length; i += 8 * 97)
        {
            BinaryPrimitives.ReadHalfLittleEndian(frame.Pixels.AsSpan(i, 2)).Should().Be(Half.One, "alpha is forced to 1.0");
        }

        var sdr = HdrToneMapper.ToSdr(frame);
        sdr.Format.Should().Be(FramePixelFormat.Bgra8);
        (sdr.Width, sdr.Height).Should().Be((frame.Width, frame.Height));
        TestFrames.IsOpaque(sdr).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_ConsecutiveCaptures_ReuseTheDevice()
    {
        var monitor = PrimaryMonitorOrSkip();
        var source = new MonitorCaptureSource(monitor.Handle);

        var first = await _engine.CaptureAsync(source, CaptureOptions.Default, Ct);
        var device = _devices.Acquire();
        var second = await _engine.CaptureAsync(source, CaptureOptions.Default, Ct);

        _devices.Acquire().Should().BeSameAs(device);
        (second.Width, second.Height).Should().Be((first.Width, first.Height));
    }

    [Fact]
    public async Task CaptureAsync_AfterTheDeviceWasInvalidated_RecreatesIt()
    {
        var monitor = PrimaryMonitorOrSkip();
        var before = _devices.Acquire();

        _devices.Invalidate();
        var frame = await _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), CaptureOptions.Default, Ct);

        frame.Width.Should().BePositive();
        _devices.Acquire().Should().NotBeSameAs(before);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x7FFF_FFFF_FFF0L)]
    public async Task CaptureAsync_WindowThatDoesNotExist_FailsWithTargetGone(long handle)
    {
        SkipUnlessCaptureIsSupported();

        var act = () => _engine.CaptureAsync(new WindowCaptureSource((nint)handle), CaptureOptions.Default, Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.TargetGone);
    }

    [Fact]
    public async Task CaptureAsync_NoMonitorHandle_FailsWithTargetGone()
    {
        SkipUnlessCaptureIsSupported();

        var act = () => _engine.CaptureAsync(new MonitorCaptureSource(0), CaptureOptions.Default, Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.TargetGone);
    }

    [Fact]
    public async Task CaptureAsync_CancelledToken_Throws()
    {
        var monitor = PrimaryMonitorOrSkip();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), CaptureOptions.Default, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CaptureAsync_NullArguments_Throw()
    {
        await FluentActions.Awaiting(() => _engine.CaptureAsync(null!, CaptureOptions.Default, Ct)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => _engine.CaptureAsync(new MonitorCaptureSource(1), null!, Ct)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void DeviceManager_AfterDispose_RefusesToHandOutADevice()
    {
        var devices = new D3D11DeviceManager(NullLogger<D3D11DeviceManager>.Instance);
        devices.Acquire().Device.Should().NotBeNull();

        devices.Dispose();
        devices.Dispose();

        devices.Invoking(d => d.Acquire()).Should().Throw<ObjectDisposedException>();
    }

    private static void SkipUnlessCaptureIsSupported()
    {
        if (!GraphicsCaptureSession.IsSupported())
        {
            Assert.Skip("Windows.Graphics.Capture is not supported on this machine.");
        }
    }

    private MonitorInfo PrimaryMonitorOrSkip()
    {
        SkipUnlessCaptureIsSupported();
        var monitors = _displays.GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (monitor is null)
        {
            Assert.Skip("No monitor is attached to this session.");
        }
        return monitor;
    }
}
