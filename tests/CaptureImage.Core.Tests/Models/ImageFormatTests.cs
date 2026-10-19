// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class ImageFormatTests
{
    [Theory]
    [InlineData(ImageFormat.Png, "png")]
    [InlineData(ImageFormat.Jpeg, "jpg")]
    [InlineData(ImageFormat.Webp, "webp")]
    [InlineData(ImageFormat.Tiff, "tiff")]
    [InlineData(ImageFormat.Jxr, "jxr")]
    public void Extension_IsLowercaseWithoutDot(ImageFormat format, string extension)
    {
        format.Extension().Should().Be(extension);
    }

    [Fact]
    public void Extension_OfAnUndefinedFormat_Throws()
    {
        var act = () => ((ImageFormat)99).Extension();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("png", ImageFormat.Png)]
    [InlineData(".PNG", ImageFormat.Png)]
    [InlineData("..png", ImageFormat.Png)]
    [InlineData("jpg", ImageFormat.Jpeg)]
    [InlineData("JPEG", ImageFormat.Jpeg)]
    [InlineData(".jpeg", ImageFormat.Jpeg)]
    [InlineData("webp", ImageFormat.Webp)]
    [InlineData("tif", ImageFormat.Tiff)]
    [InlineData(".TIFF", ImageFormat.Tiff)]
    [InlineData("jxr", ImageFormat.Jxr)]
    [InlineData(".wdp", ImageFormat.Jxr)]
    [InlineData("gif", null)]
    [InlineData(".bmp", null)]
    [InlineData("png ", null)]
    [InlineData(".", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FromExtension_AcceptsCommonSpellings(string? extension, ImageFormat? expected)
    {
        ImageFormatExtensions.FromExtension(extension).Should().Be(expected);
    }

    [Fact]
    public void FromExtension_RoundTripsEveryFormat()
    {
        foreach (var format in Enum.GetValues<ImageFormat>())
        {
            ImageFormatExtensions.FromExtension(format.Extension()).Should().Be(format);
            ImageFormatExtensions.FromExtension("." + format.Extension()).Should().Be(format);
        }
    }

    [Fact]
    public void UserSelectable_OffersEveryFormatExceptTheHdrSidecar()
    {
        ImageFormatExtensions.UserSelectable.Should().Equal(ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Webp, ImageFormat.Tiff);
        ImageFormatExtensions.UserSelectable.Should().NotContain(ImageFormat.Jxr);
    }
}
