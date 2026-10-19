// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using Xunit;

namespace CaptureImage.Core.Tests.Capture;

public class CaptureModelTests
{
    [Fact]
    public void HdrInfoSdr_DescribesAPlain80NitDisplay()
    {
        HdrInfo.ScRgbReferenceWhiteNits.Should().Be(80f);
        HdrInfo.Sdr.Should().Be(new HdrInfo(false, 80f, 80f));
    }

    [Fact]
    public void CaptureOptionsDefault_CapturesAnSdrClientAreaWithoutCursor()
    {
        CaptureOptions.Default.Should().Be(new CaptureOptions(PreferHdr: false, IncludeCursor: false, IncludeWindowFrame: false));
    }

    [Theory]
    [InlineData(96u, 1.0)]
    [InlineData(120u, 1.25)]
    [InlineData(144u, 1.5)]
    [InlineData(192u, 2.0)]
    public void MonitorScale_IsRelativeTo96Dpi(uint dpi, double scale)
    {
        var monitor = new MonitorInfo(1, @"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), dpi, true, HdrInfo.Sdr);

        monitor.Scale.Should().Be(scale);
    }

    [Fact]
    public void CaptureSources_CompareByHandle()
    {
        new WindowCaptureSource(42).Should().Be(new WindowCaptureSource(42));
        new MonitorCaptureSource(42).Should().NotBe(new WindowCaptureSource(42));
    }
}
