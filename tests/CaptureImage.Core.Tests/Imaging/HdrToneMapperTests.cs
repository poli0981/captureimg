// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Imaging;

public class HdrToneMapperTests
{
    private static readonly HdrInfo Hdr240 = new(IsHdrActive: true, SdrWhiteNits: 240f, MaxLuminanceNits: 1000f);

    [Fact]
    public void SdrFrame_IsReturnedUnchanged()
    {
        var frame = Frames.SolidBgra(2, 2, new BgraPixel(1, 2, 3, 255));

        HdrToneMapper.ToSdr(frame).Should().BeSameAs(frame);
    }

    [Fact]
    public void HdrFrame_BecomesAnOpaqueTightBgra8FrameOfTheSameSize()
    {
        var frame = Frames.CreateHalf(5, 3, (x, y) => new RgbaPixel(x * 0.5f, y * 0.25f, 0.1f, 0f), paddingPixels: 2, hdr: Hdr240);

        var sdr = HdrToneMapper.ToSdr(frame);

        sdr.Format.Should().Be(FramePixelFormat.Bgra8);
        sdr.Width.Should().Be(5);
        sdr.Height.Should().Be(3);
        sdr.RowStride.Should().Be(5 * 4);
        sdr.Pixels.Should().HaveCount(5 * 4 * 3);
        sdr.IsHdr.Should().BeFalse();
        AllPixels(sdr).Should().OnlyContain(p => p.A == 255, "source alpha is ignored; screenshots are opaque");
    }

    [Fact]
    public void Black_StaysBlack()
    {
        var sdr = HdrToneMapper.ToSdr(Frames.SolidHalf(3, 2, new RgbaPixel(0f, 0f, 0f, 1f), Hdr240));

        AllPixels(sdr).Should().OnlyContain(p => p == new BgraPixel(0, 0, 0, 255));
    }

    [Fact]
    public void NegativeComponents_AreClippedToZero()
    {
        var pixel = ToneMapOne(new RgbaPixel(-0.5f, 0.5f, -2f, 1f), HdrInfo.Sdr);

        pixel.R.Should().Be(0);
        pixel.B.Should().Be(0);
        pixel.G.Should().BeCloseTo(SrgbByte(0.5f), 1);
    }

    [Fact]
    public void Channels_AreWrittenInBgraOrder()
    {
        var red = ToneMapOne(new RgbaPixel(0.5f, 0f, 0f, 1f), HdrInfo.Sdr);
        var blue = ToneMapOne(new RgbaPixel(0f, 0f, 0.5f, 1f), HdrInfo.Sdr);

        red.Should().Be(new BgraPixel(0, 0, red.R, 255));
        red.R.Should().BeCloseTo(SrgbByte(0.5f), 1);
        blue.Should().Be(new BgraPixel(blue.B, 0, 0, 255));
        blue.B.Should().Be(red.R);
    }

    [Theory]
    [InlineData(80f)]
    [InlineData(203f)]
    [InlineData(240f)]
    [InlineData(480f)]
    public void SdrReferenceWhite_MapsNear255_WhateverTheDisplaysWhiteLevel(float sdrWhiteNits)
    {
        // A pixel at the display's "SDR content brightness" is what an SDR app's white looked like.
        var white = sdrWhiteNits / HdrInfo.ScRgbReferenceWhiteNits;

        var pixel = ToneMapOne(new RgbaPixel(white, white, white, 1f), new HdrInfo(true, sdrWhiteNits, 1000f));

        pixel.R.Should().BeGreaterThanOrEqualTo(240);
        pixel.R.Should().BeCloseTo(SrgbByte(HdrToneMapper.CompressHighlight(1f)), 1);
        pixel.G.Should().Be(pixel.R);
        pixel.B.Should().Be(pixel.R);
    }

    [Theory]
    [InlineData(false, 0f)]
    [InlineData(true, 0f)]
    [InlineData(true, 1f)]
    public void MissingOrUnusableWhiteLevel_FallsBackTo80Nits(bool attachInfo, float sdrWhiteNits)
    {
        var info = attachInfo ? new HdrInfo(true, sdrWhiteNits, 1000f) : null;
        var expected = ToneMapOne(new RgbaPixel(0.5f, 0.5f, 0.5f, 1f), HdrInfo.Sdr);

        var actual = ToneMapOne(new RgbaPixel(0.5f, 0.5f, 0.5f, 1f), info);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.001f)]
    [InlineData(0.05f)]
    [InlineData(0.18f)]
    [InlineData(0.5f)]
    [InlineData(0.79f)]
    public void BelowTheKnee_IsPlainSrgbEncoding(float linear)
    {
        var pixel = ToneMapOne(new RgbaPixel(linear, linear, linear, 1f), HdrInfo.Sdr);

        pixel.G.Should().BeCloseTo(SrgbByte(linear), 1);
    }

    [Fact]
    public void Highlights_CompressMonotonically_WithoutClipping()
    {
        float[] luminance = [0.5f, 0.79f, 0.8f, 0.9f, 1f, 1.2f, 1.5f, 2f, 3f, 4f, 8f, 12.5f, 100f, 1000f, 65504f];
        var frame = Frames.CreateHalf(luminance.Length, 1, (x, _) => new RgbaPixel(luminance[x], luminance[x], luminance[x], 1f));

        var sdr = HdrToneMapper.ToSdr(frame);
        var gray = Enumerable.Range(0, luminance.Length).Select(x => Frames.ReadBgra(sdr, x, 0).G).ToList();

        gray.Should().BeInAscendingOrder("a brighter input never comes out darker");
        gray.Take(7).Should().OnlyHaveUniqueItems("highlights up to 1.5x SDR white stay distinguishable instead of clipping");
        gray[^1].Should().Be(255);
        gray[4].Should().BeLessThan(255, "SDR white sits below the top of the curve, leaving room for highlights");
    }

    [Fact]
    public void BrightSaturatedColor_KeepsItsHue()
    {
        // Clipping each channel separately would turn (4, 2, 1) into white; scaling all three
        // by the same factor keeps orange orange.
        var pixel = ToneMapOne(new RgbaPixel(4f, 2f, 1f, 1f), HdrInfo.Sdr);
        var factor = HdrToneMapper.CompressHighlight(4f) / 4f;

        pixel.R.Should().BeGreaterThan(pixel.G);
        pixel.G.Should().BeGreaterThan(pixel.B);
        pixel.B.Should().BeGreaterThan(0);
        pixel.R.Should().BeCloseTo(SrgbByte(4f * factor), 1);
        pixel.G.Should().BeCloseTo(SrgbByte(2f * factor), 1);
        pixel.B.Should().BeCloseTo(SrgbByte(1f * factor), 1);
    }

    [Fact]
    public void RowPadding_IsNeverRead()
    {
        // The padding holds 65504 — if it leaked into the output the black image would turn white.
        var frame = Frames.CreateHalf(2, 3, (_, _) => new RgbaPixel(0f, 0f, 0f, 1f), paddingPixels: 3);

        AllPixels(HdrToneMapper.ToSdr(frame)).Should().OnlyContain(p => p == new BgraPixel(0, 0, 0, 255));
    }

    [Fact]
    public void Null_Throws()
    {
        var act = () => HdrToneMapper.ToSdr(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- the curve itself ------------------------------------------------------------------

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.8f)]
    public void CompressHighlight_IsIdentityUpToTheKnee(float value)
    {
        HdrToneMapper.CompressHighlight(value).Should().Be(value);
    }

    [Fact]
    public void CompressHighlight_JoinsTheIdentitySmoothlyAtTheKnee()
    {
        const float h = 1e-3f;
        var knee = HdrToneMapper.KneeStart;

        var slope = (HdrToneMapper.CompressHighlight(knee + h) - HdrToneMapper.CompressHighlight(knee)) / h;

        knee.Should().Be(0.8f);
        slope.Should().BeApproximately(1f, 0.01f, "the shoulder is C1-continuous with the identity");
    }

    [Fact]
    public void CompressHighlight_RisesTowardsOneWithoutReachingPastIt()
    {
        float[] inputs = [0.8f, 0.85f, 0.9f, 1f, 1.25f, 1.5f, 2f, 3f];

        var outputs = inputs.Select(HdrToneMapper.CompressHighlight).ToList();

        outputs.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        outputs.Should().OnlyContain(v => v < 1f);
        HdrToneMapper.CompressHighlight(1000f).Should().BeLessThanOrEqualTo(1f);
        HdrToneMapper.CompressHighlight(2f).Should().BeGreaterThan(0.99f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.001f, 0.01292f)]
    [InlineData(0.0031308f, 0.04045f)]
    [InlineData(0.18f, 0.46136f)]
    [InlineData(0.5f, 0.73536f)]
    [InlineData(1f, 1f)]
    public void LinearToSrgb_FollowsTheSrgbTransferFunction(float linear, float encoded)
    {
        HdrToneMapper.LinearToSrgb(linear).Should().BeApproximately(encoded, 1e-4f);
    }

    [Fact]
    public void LinearToSrgb_IsContinuousWhereTheLinearSegmentEnds()
    {
        const float threshold = 0.0031308f;

        var below = HdrToneMapper.LinearToSrgb(threshold - 1e-7f);
        var above = HdrToneMapper.LinearToSrgb(threshold + 1e-7f);

        above.Should().BeApproximately(below, 1e-5f);
    }

    private static BgraPixel ToneMapOne(RgbaPixel pixel, HdrInfo? hdr) =>
        Frames.ReadBgra(HdrToneMapper.ToSdr(Frames.SolidHalf(1, 1, pixel, hdr)), 0, 0);

    private static byte SrgbByte(float linear) => (byte)MathF.Round(HdrToneMapper.LinearToSrgb(linear) * 255f);

    private static IEnumerable<BgraPixel> AllPixels(CapturedFrame frame) =>
        from y in Enumerable.Range(0, frame.Height)
        from x in Enumerable.Range(0, frame.Width)
        select Frames.ReadBgra(frame, x, y);
}
