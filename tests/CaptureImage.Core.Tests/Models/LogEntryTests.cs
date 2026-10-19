// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using AwesomeAssertions;
using CaptureImage.Core.Models;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class LogEntryTests
{
    private static LogEntry MakeEntry(
        LogEntryLevel level = LogEntryLevel.Information,
        string source = "CaptureImage.Core.Pipeline.CaptureCoordinator",
        string message = "hello") =>
        new(
            Timestamp: new DateTimeOffset(2026, 4, 26, 14, 30, 5, 123, TimeSpan.FromHours(7)),
            Level: level,
            SourceContext: source,
            Message: message,
            Exception: null);

    [Fact]
    public void TimestampText_IsTheEventsWallClockWithMilliseconds()
    {
        MakeEntry().TimestampText.Should().Be("14:30:05.123");
    }

    [Theory]
    [InlineData("fi-FI")]
    [InlineData("ar-SA")]
    [InlineData("vi-VN")]
    public void TimestampText_IsCultureInvariant(string cultureName)
    {
        // The log viewer is technical content: every UI language shows the same timestamps.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            MakeEntry().TimestampText.Should().Be("14:30:05.123");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(LogEntryLevel.Verbose, "VRB")]
    [InlineData(LogEntryLevel.Debug, "DBG")]
    [InlineData(LogEntryLevel.Information, "INF")]
    [InlineData(LogEntryLevel.Warning, "WRN")]
    [InlineData(LogEntryLevel.Error, "ERR")]
    [InlineData(LogEntryLevel.Fatal, "FTL")]
    [InlineData((LogEntryLevel)42, "FTL")]
    public void LevelText_IsTheThreeLetterCode(LogEntryLevel level, string expected)
    {
        MakeEntry(level).LevelText.Should().Be(expected);
    }

    [Theory]
    [InlineData("CaptureImage.Core.Pipeline.CaptureCoordinator", "CaptureCoordinator")]
    [InlineData("Program", "Program")]
    [InlineData("", "")]
    [InlineData("Trailing.", "")]
    public void ShortSource_DropsTheNamespace(string source, string expected)
    {
        MakeEntry(source: source).ShortSource.Should().Be(expected);
    }

    [Fact]
    public void ToString_IsLevelAndMessage_ForScreenReaders()
    {
        MakeEntry(LogEntryLevel.Warning, message: "disk almost full").ToString().Should().Be("Warning: disk almost full");
    }
}
