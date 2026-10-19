// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Infrastructure.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Windowing;

/// <summary><see cref="IconExtractor"/>: executable icons as PNG bytes, cached per path.</summary>
public sealed class IconExtractorTests
{
    private static readonly string Explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    private readonly IconExtractor _icons = new(NullLogger<IconExtractor>.Instance);

    [Fact]
    public void TryGetIconPngBytes_Executable_ReturnsADecodablePng()
    {
        var png = _icons.TryGetIconPngBytes(Explorer);

        png.Should().NotBeNull();
        png[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        using var bitmap = SKBitmap.Decode(png);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().BePositive();
    }

    [Fact]
    public void TryGetIconPngBytes_SamePathInAnyCase_IsServedFromTheCache()
    {
        var first = _icons.TryGetIconPngBytes(Explorer);

        _icons.TryGetIconPngBytes(Explorer).Should().BeSameAs(first);
        _icons.TryGetIconPngBytes(Explorer.ToUpperInvariant()).Should().BeSameAs(first);
    }

    [Fact]
    public void TryGetIconPngBytes_MissingFile_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");

        _icons.TryGetIconPngBytes(missing).Should().BeNull();
        _icons.TryGetIconPngBytes(missing).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void TryGetIconPngBytes_BlankPath_ReturnsNull(string path)
    {
        _icons.TryGetIconPngBytes(path).Should().BeNull();
    }
}
