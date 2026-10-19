// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class CapturedFrameTests
{
    [Fact]
    public void Bgra8_HasFourBytesPerPixel()
    {
        var frame = Frames.SolidBgra(5, 2, new BgraPixel(1, 2, 3, 4), paddingBytes: 12);

        frame.BytesPerPixel.Should().Be(4);
        frame.IsHdr.Should().BeFalse();
        frame.TightStride.Should().Be(20);
        frame.RowStride.Should().Be(32);
        frame.IsTightlyPacked.Should().BeFalse();
    }

    [Fact]
    public void RgbaHalf_HasEightBytesPerPixel()
    {
        var frame = Frames.SolidHalf(3, 2, new RgbaPixel(1f, 1f, 1f, 1f));

        frame.BytesPerPixel.Should().Be(8);
        frame.IsHdr.Should().BeTrue();
        frame.TightStride.Should().Be(24);
        frame.IsTightlyPacked.Should().BeTrue();
    }

    [Fact]
    public void ToTightlyPacked_ReturnsTheSameArray_WhenAlreadyTight()
    {
        var frame = Frames.SolidBgra(4, 3, new BgraPixel(1, 2, 3, 255));

        frame.ToTightlyPacked().Should().BeSameAs(frame.Pixels);
    }

    [Fact]
    public void ToTightlyPacked_StripsRowPadding()
    {
        var frame = Frames.CreateBgra(3, 2, (x, y) => new BgraPixel((byte)x, (byte)y, 7, 255), paddingBytes: 5);

        var tight = frame.ToTightlyPacked();

        tight.Should().Equal(
            0, 0, 7, 255, 1, 0, 7, 255, 2, 0, 7, 255,
            0, 1, 7, 255, 1, 1, 7, 255, 2, 1, 7, 255);
    }

    [Fact]
    public void ToTightlyPacked_CopiesWhenTheBufferHasTrailingBytes()
    {
        var pixels = new byte[(2 * 4) + 3];
        var frame = new CapturedFrame(2, 1, 8, pixels);

        var tight = frame.ToTightlyPacked();

        frame.IsTightlyPacked.Should().BeTrue();
        tight.Should().HaveCount(8).And.NotBeSameAs(pixels);
    }
}
