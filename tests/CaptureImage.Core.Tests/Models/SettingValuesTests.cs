// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class SettingValuesTests
{
    [Theory]
    [InlineData("None", ClipboardMode.None)]
    [InlineData("copy", ClipboardMode.Copy)]
    [InlineData("CopyAndSave", ClipboardMode.CopyAndSave)]
    [InlineData("1", ClipboardMode.Copy)]
    [InlineData("99", ClipboardMode.None)]
    [InlineData("Clipboard", ClipboardMode.None)]
    [InlineData("", ClipboardMode.None)]
    [InlineData(null, ClipboardMode.None)]
    public void ParseClipboardMode_FallsBackToFileOnly(string? value, ClipboardMode expected)
    {
        SettingValues.ParseClipboardMode(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("fast", PngCompressionLevel.Fast)]
    [InlineData("Balanced", PngCompressionLevel.Balanced)]
    [InlineData("SMALLEST", PngCompressionLevel.Smallest)]
    [InlineData("-1", PngCompressionLevel.Balanced)]
    [InlineData("max", PngCompressionLevel.Balanced)]
    [InlineData(null, PngCompressionLevel.Balanced)]
    public void ParsePngCompression_FallsBackToBalanced(string? value, PngCompressionLevel expected)
    {
        SettingValues.ParsePngCompression(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Off", false)]
    [InlineData("off", false)]
    [InlineData("Auto", true)]
    [InlineData("On", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void IsHdrEnabled_UnlessExplicitlyOff(string? value, bool expected)
    {
        SettingValues.IsHdrEnabled(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(2, 0)]
    [InlineData(11, 0)]
    [InlineData(-5, 0)]
    public void NormalizeCountdown_OnlyKeepsOfferedChoices(int seconds, int expected)
    {
        SettingValues.NormalizeCountdown(seconds).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(250, 100)]
    public void ClampQuality_KeepsOneThroughHundred(int value, int expected)
    {
        SettingValues.ClampQuality(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("debug", "Debug")]
    [InlineData("Warning", "Warning")]
    [InlineData("info", "Information")]
    [InlineData(null, "Information")]
    public void NormalizeLogLevel_ReturnsTheCanonicalSpelling(string? value, string expected)
    {
        SettingValues.NormalizeLogLevel(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("light", "Light")]
    [InlineData("HighContrast", "HighContrast")]
    [InlineData("Solarized", "System")]
    [InlineData(null, "System")]
    public void NormalizeTheme_ReturnsTheCanonicalSpelling(string? value, string expected)
    {
        SettingValues.NormalizeTheme(value).Should().Be(expected);
    }

    [Fact]
    public void Choices_AreTheDocumentedOnes()
    {
        SettingValues.CountdownChoices.Should().Equal(0, 3, 5, 10);
        SettingValues.LogLevels.Should().Equal("Debug", "Information", "Warning", "Error");
        SettingValues.Themes.Should().Equal("System", "Light", "Dark", "HighContrast");
    }
}
