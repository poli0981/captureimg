// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using Xunit;

namespace CaptureImage.Core.Tests.Capture;

public class PixelRectTests
{
    [Fact]
    public void RightAndBottom_AreExclusiveEdges()
    {
        var rect = new PixelRect(10, 20, 30, 40);

        rect.Right.Should().Be(40);
        rect.Bottom.Should().Be(60);
    }

    [Theory]
    [InlineData(0, 0, 0, 5, true)]
    [InlineData(0, 0, 5, 0, true)]
    [InlineData(0, 0, -1, 5, true)]
    [InlineData(0, 0, 5, -1, true)]
    [InlineData(-5, -5, 1, 1, false)]
    [InlineData(0, 0, 1, 1, false)]
    public void IsEmpty_WhenWidthOrHeightIsNotPositive(int x, int y, int width, int height, bool expected)
    {
        new PixelRect(x, y, width, height).IsEmpty.Should().Be(expected);
    }

    [Fact]
    public void Default_IsEmpty()
    {
        default(PixelRect).IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 10, 10, 5, 5, 10, 10, 5, 5, 5, 5)]           // partial overlap
    [InlineData(0, 0, 10, 10, 2, 3, 4, 5, 2, 3, 4, 5)]             // contained
    [InlineData(-10, -10, 15, 15, 0, 0, 10, 10, 0, 0, 5, 5)]       // negative coordinates
    [InlineData(0, 0, 1920, 1080, -1920, 0, 3840, 1080, 0, 0, 1920, 1080)] // spans the monitor
    public void Intersect_ReturnsTheOverlap_InEitherOrder(
        int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh, int ex, int ey, int ew, int eh)
    {
        var a = new PixelRect(ax, ay, aw, ah);
        var b = new PixelRect(bx, by, bw, bh);
        var expected = new PixelRect(ex, ey, ew, eh);

        a.Intersect(b).Should().Be(expected);
        b.Intersect(a).Should().Be(expected);
    }

    [Theory]
    [InlineData(10, 0, 5, 5)]   // touches the right edge
    [InlineData(0, 10, 5, 5)]   // touches the bottom edge
    [InlineData(-5, 0, 5, 5)]   // touches the left edge
    [InlineData(20, 20, 5, 5)]  // far away
    [InlineData(2, 2, 0, 0)]    // empty rectangle inside
    public void Intersect_WithoutOverlap_IsEmpty(int x, int y, int width, int height)
    {
        var result = new PixelRect(0, 0, 10, 10).Intersect(new PixelRect(x, y, width, height));

        result.Should().Be(default(PixelRect));
        result.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(1930, 100, 1920, 0, 10, 100)]   // monitor to the right of the primary
    [InlineData(-1500, 200, -1920, 0, 420, 200)] // monitor to the left (negative origin)
    [InlineData(5, 5, 0, 0, 5, 5)]
    public void RelativeTo_ShiftsTheOriginAndKeepsTheSize(int x, int y, int originX, int originY, int expectedX, int expectedY)
    {
        var rect = new PixelRect(x, y, 50, 60);

        rect.RelativeTo(new PixelRect(originX, originY, 2560, 1440)).Should().Be(new PixelRect(expectedX, expectedY, 50, 60));
    }

    [Theory]
    [InlineData(10, 20, true)]  // top-left corner is inside
    [InlineData(39, 59, true)]  // last pixel
    [InlineData(25, 40, true)]
    [InlineData(40, 20, false)] // right edge is exclusive
    [InlineData(10, 60, false)] // bottom edge is exclusive
    [InlineData(9, 20, false)]
    [InlineData(10, 19, false)]
    public void Contains_IncludesTopLeft_ExcludesRightAndBottom(int x, int y, bool expected)
    {
        new PixelRect(10, 20, 30, 40).Contains(x, y).Should().Be(expected);
    }

    [Fact]
    public void ToString_ShowsSizeThenOrigin()
    {
        new PixelRect(10, -20, 30, 40).ToString().Should().Be("30x40@(10,-20)");
    }
}
