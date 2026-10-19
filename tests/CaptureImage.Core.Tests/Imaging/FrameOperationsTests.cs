// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Buffers.Binary;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Imaging;

public class FrameOperationsTests
{
    /// <summary>Encodes the pixel's own coordinates so a copy from the wrong place is obvious.</summary>
    private static BgraPixel Coded(int x, int y) => new((byte)x, (byte)y, 0x80, 0xFF);

    // ---- Crop -----------------------------------------------------------------------------

    [Fact]
    public void Crop_Bgra8_CopiesTheAreaIntoATightlyPackedFrame()
    {
        var frame = Frames.CreateBgra(6, 4, Coded, paddingBytes: 8);

        var crop = FrameOperations.Crop(frame, new PixelRect(1, 2, 3, 2));

        crop.Format.Should().Be(FramePixelFormat.Bgra8);
        crop.Width.Should().Be(3);
        crop.Height.Should().Be(2);
        crop.RowStride.Should().Be(3 * 4);
        crop.Pixels.Should().HaveCount(3 * 4 * 2);
        crop.IsTightlyPacked.Should().BeTrue();
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Frames.ReadBgra(crop, x, y).Should().Be(Coded(x + 1, y + 2));
            }
        }
    }

    [Fact]
    public void Crop_RgbaHalf_CopiesTheAreaAndKeepsTheHdrInfo()
    {
        var hdr = new HdrInfo(true, 240f, 1000f);
        var frame = Frames.CreateHalf(4, 3, (x, y) => new RgbaPixel(x, y, x + y, 1f), paddingPixels: 2, hdr: hdr);

        var crop = FrameOperations.Crop(frame, new PixelRect(2, 1, 2, 2));

        crop.Format.Should().Be(FramePixelFormat.RgbaHalf);
        crop.Width.Should().Be(2);
        crop.Height.Should().Be(2);
        crop.RowStride.Should().Be(2 * 8);
        crop.Pixels.Should().HaveCount(2 * 8 * 2);
        crop.Hdr.Should().BeSameAs(hdr);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                Frames.ReadHalf(crop, x, y).Should().Be(new RgbaPixel(x + 2, y + 1, x + y + 3, 1f));
            }
        }
    }

    [Fact]
    public void Crop_WholeFrame_DropsTheRowPadding()
    {
        var frame = Frames.CreateBgra(5, 3, Coded, paddingBytes: 12);

        var crop = FrameOperations.Crop(frame, new PixelRect(0, 0, 5, 3));

        crop.Pixels.Should().Equal(frame.ToTightlyPacked());
        crop.Pixels.Should().NotContain(Frames.PaddingByte);
    }

    [Fact]
    public void Crop_ReturnsACopy()
    {
        var frame = Frames.CreateBgra(2, 2, Coded);

        var crop = FrameOperations.Crop(frame, new PixelRect(0, 0, 2, 2));
        frame.Pixels[0] = 0x55;

        crop.Pixels.Should().NotBeSameAs(frame.Pixels);
        Frames.ReadBgra(crop, 0, 0).Should().Be(Coded(0, 0));
    }

    [Theory]
    [InlineData(4, 2, 10, 10, 4, 2, 2, 2)]     // runs past the right and bottom edges
    [InlineData(-2, -1, 4, 3, 0, 0, 2, 2)]     // starts above and left of the frame
    [InlineData(-5, -5, 100, 100, 0, 0, 6, 4)] // covers the whole frame
    [InlineData(5, 3, 1, 1, 5, 3, 1, 1)]       // the last pixel
    public void Crop_AreaReachingOutside_IsClippedToTheFrame(
        int x, int y, int width, int height, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var frame = Frames.CreateBgra(6, 4, Coded, paddingBytes: 4);

        var crop = FrameOperations.Crop(frame, new PixelRect(x, y, width, height));

        crop.Width.Should().Be(expectedWidth);
        crop.Height.Should().Be(expectedHeight);
        Frames.ReadBgra(crop, 0, 0).Should().Be(Coded(expectedX, expectedY));
        Frames.ReadBgra(crop, expectedWidth - 1, expectedHeight - 1)
            .Should().Be(Coded(expectedX + expectedWidth - 1, expectedY + expectedHeight - 1));
    }

    [Theory]
    [InlineData(6, 0, 2, 2)]   // right of the frame
    [InlineData(0, 4, 1, 1)]   // below the frame
    [InlineData(-3, 0, 3, 2)]  // ends exactly at the left edge
    [InlineData(1, 1, 0, 2)]   // zero width
    [InlineData(1, 1, 2, -1)]  // negative height
    public void Crop_AreaWithoutOverlap_Throws(int x, int y, int width, int height)
    {
        var frame = Frames.CreateBgra(6, 4, Coded);

        var act = () => FrameOperations.Crop(frame, new PixelRect(x, y, width, height));

        act.Should().Throw<ArgumentException>().WithParameterName("area");
    }

    [Fact]
    public void Crop_Null_Throws()
    {
        var act = () => FrameOperations.Crop(null!, new PixelRect(0, 0, 1, 1));

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- ForceOpaque ------------------------------------------------------------------------

    [Fact]
    public void ForceOpaque_Bgra8_SetsOnlyTheAlphaBytes()
    {
        var frame = Frames.CreateBgra(3, 2, (x, y) => new BgraPixel((byte)(10 + x), (byte)(20 + y), 30, (byte)(x * 100)), paddingBytes: 4);

        FrameOperations.ForceOpaque(frame);

        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Frames.ReadBgra(frame, x, y).Should().Be(new BgraPixel((byte)(10 + x), (byte)(20 + y), 30, 255));
            }

            Frames.RowPadding(frame, y).Should().OnlyContain(b => b == Frames.PaddingByte, "row padding is not pixel data");
        }
    }

    [Fact]
    public void ForceOpaque_RgbaHalf_SetsAlphaToOnePointZero()
    {
        var frame = Frames.CreateHalf(3, 2, (x, y) => new RgbaPixel(x, y, 0.5f, 0.25f), paddingPixels: 1);

        FrameOperations.ForceOpaque(frame);

        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Frames.ReadHalf(frame, x, y).Should().Be(new RgbaPixel(x, y, 0.5f, 1f));
                var alpha = frame.Pixels.AsSpan((y * frame.RowStride) + (x * 8) + 6, 2).ToArray();
                alpha.Should().Equal(0x00, 0x3C);
            }

            var padding = Frames.RowPadding(frame, y);
            BinaryPrimitives.ReadHalfLittleEndian(padding.AsSpan(6)).Should().Be(Frames.PaddingHalf, "row padding is not pixel data");
        }
    }

    [Fact]
    public void ForceOpaque_Null_Throws()
    {
        var act = () => FrameOperations.ForceOpaque(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
