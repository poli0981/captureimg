using System;
using CaptureImage.Core.Models;
using FluentAssertions;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

public class LogEntryTests
{
    private static LogEntry MakeEntry(string? file = null, int? line = null) =>
        new(
            Timestamp: new DateTimeOffset(2026, 4, 26, 14, 30, 5, 123, TimeSpan.Zero),
            Level: LogLevel.Information,
            SourceContext: "Test",
            Message: "hello",
            Exception: null)
        {
            File = file,
            Line = line,
        };

    [Fact]
    public void TimestampText_FormatsAsHHmmssfff()
    {
        // Added in v1.3-M5 because WinUI 3 {Binding} doesn't support StringFormat.
        // The format must be culture-invariant so vi-VN / ar-SA users get the same
        // timestamps as en-US (the log viewer is technical content, not localized).
        MakeEntry().TimestampText.Should().Be("14:30:05.123");
    }

    [Fact]
    public void FileLineText_BothPresent_RendersFileColonLine()
    {
        MakeEntry(file: "Foo.cs", line: 42).FileLineText.Should().Be("Foo.cs:42");
    }

    [Fact]
    public void FileLineText_FileMissing_RendersEmpty()
    {
        MakeEntry(file: null, line: 42).FileLineText.Should().BeEmpty();
    }

    [Fact]
    public void FileLineText_LineMissing_RendersEmpty()
    {
        MakeEntry(file: "Foo.cs", line: null).FileLineText.Should().BeEmpty();
    }

    [Fact]
    public void FileLineText_LineZero_RendersEmpty()
    {
        // Line is uint? — 0 is a sentinel for "unknown" since real source lines are 1+.
        MakeEntry(file: "Foo.cs", line: 0).FileLineText.Should().BeEmpty();
    }
}
