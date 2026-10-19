// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using AwesomeAssertions;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Logging;

/// <summary>
/// <see cref="InMemorySink"/>, the log viewer's ring buffer: capacity and exception-size
/// bounds, pause/clear, snapshot isolation and how messages are rendered.
/// </summary>
public class InMemorySinkTests
{
    /// <summary>InMemorySink.MaxExceptionLength (private).</summary>
    private const int MaxExceptionLength = 8_192;

    [Fact]
    public void Emit_BuffersTheEntry_AndRaisesEmitted()
    {
        var sink = new InMemorySink();
        var raised = new List<LogEntry>();
        sink.Emitted += (_, entry) => raised.Add(entry);
        var timestamp = new DateTimeOffset(2026, 10, 19, 7, 15, 0, TimeSpan.FromHours(7));

        sink.Emit(Event("hello", LogEventLevel.Information, timestamp: timestamp));

        var entry = sink.Snapshot().Should().ContainSingle().Subject;
        entry.Message.Should().Be("hello");
        entry.Level.Should().Be(LogEntryLevel.Information);
        entry.Timestamp.Should().Be(timestamp);
        entry.SourceContext.Should().BeEmpty();
        entry.Exception.Should().BeNull();
        raised.Should().ContainSingle().Which.Should().BeSameAs(entry);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, LogEntryLevel.Verbose)]
    [InlineData(LogEventLevel.Debug, LogEntryLevel.Debug)]
    [InlineData(LogEventLevel.Information, LogEntryLevel.Information)]
    [InlineData(LogEventLevel.Warning, LogEntryLevel.Warning)]
    [InlineData(LogEventLevel.Error, LogEntryLevel.Error)]
    [InlineData(LogEventLevel.Fatal, LogEntryLevel.Fatal)]
    public void Emit_MapsEverySerilogLevel(LogEventLevel level, LogEntryLevel expected)
    {
        var sink = new InMemorySink();

        sink.Emit(Event("x", level));

        sink.Snapshot().Should().ContainSingle().Which.Level.Should().Be(expected);
    }

    [Fact]
    public void Emit_BeyondCapacity_DropsTheOldestEntries()
    {
        var sink = new InMemorySink();

        for (var i = 0; i < InMemorySink.MaxCapacity + 5; i++)
        {
            sink.Emit(Event("msg-" + i, LogEventLevel.Information));
        }

        var snapshot = sink.Snapshot();
        snapshot.Should().HaveCount(InMemorySink.MaxCapacity);
        snapshot[0].Message.Should().Be("msg-5");
        snapshot[^1].Message.Should().Be("msg-" + (InMemorySink.MaxCapacity + 4));
    }

    [Fact]
    public void Emit_FromManyThreads_StaysWithinCapacity()
    {
        var sink = new InMemorySink();
        var raised = 0;
        sink.Emitted += (_, _) => Interlocked.Increment(ref raised);

        Parallel.For(0, 4 * InMemorySink.MaxCapacity, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken },
            i => sink.Emit(Event("m" + i, LogEventLevel.Debug)));

        sink.Snapshot().Should().HaveCount(InMemorySink.MaxCapacity);
        raised.Should().Be(4 * InMemorySink.MaxCapacity);
    }

    [Fact]
    public void Emit_LongExceptionText_IsTruncated()
    {
        var sink = new InMemorySink();
        var exception = new InvalidOperationException(new string('x', 3 * MaxExceptionLength));

        sink.Emit(Event("boom", LogEventLevel.Error, exception));

        var text = sink.Snapshot()[0].Exception;
        text.Should().NotBeNull();
        text.Should().StartWith(exception.ToString()[..MaxExceptionLength]);
        text.Should().EndWith(Environment.NewLine + "…");
        text.Should().HaveLength(MaxExceptionLength + Environment.NewLine.Length + 1);
    }

    [Fact]
    public void Emit_ShortExceptionText_IsKeptWhole()
    {
        var sink = new InMemorySink();
        InvalidOperationException exception;
        try
        {
            throw new InvalidOperationException("short failure");
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        sink.Emit(Event("failed", LogEventLevel.Warning, exception));

        sink.Snapshot()[0].Exception.Should().Be(exception.ToString());
    }

    [Fact]
    public void Paused_SuppressesEmitted_ButStillBuffers()
    {
        var sink = new InMemorySink { Paused = true };
        var raised = new List<LogEntry>();
        sink.Emitted += (_, entry) => raised.Add(entry);

        sink.Emit(Event("while paused", LogEventLevel.Warning));

        sink.Paused.Should().BeTrue();
        sink.Snapshot().Should().ContainSingle().Which.Message.Should().Be("while paused");
        raised.Should().BeEmpty();

        sink.Paused = false;
        sink.Emit(Event("after resume", LogEventLevel.Warning));

        raised.Should().ContainSingle().Which.Message.Should().Be("after resume");
        sink.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void Clear_EmptiesTheBuffer_AndLaterEntriesAreKept()
    {
        var sink = new InMemorySink();
        sink.Emit(Event("x", LogEventLevel.Information));
        sink.Emit(Event("y", LogEventLevel.Information));

        sink.Clear();
        sink.Snapshot().Should().BeEmpty();

        sink.Emit(Event("z", LogEventLevel.Information));
        sink.Snapshot().Should().ContainSingle().Which.Message.Should().Be("z");
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        var sink = new InMemorySink();
        sink.Emit(Event("a", LogEventLevel.Information));

        var first = sink.Snapshot();
        sink.Emit(Event("b", LogEventLevel.Information));

        first.Should().ContainSingle().Which.Message.Should().Be("a");
        sink.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void Message_RendersStringArgumentsWithoutQuotes_LikeTheLogFile()
    {
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        logger.Information("Saved {FileName} to {Folder} ({Count} files)", "shot.png", @"D:\Captures", 3);

        sink.Snapshot().Should().ContainSingle().Which.Message.Should().Be(@"Saved shot.png to D:\Captures (3 files)");
    }

    [Fact]
    public void Message_IsRenderedWithTheInvariantCulture()
    {
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            logger.Information("Encoded in {Seconds} s", 1.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }

        sink.Snapshot()[0].Message.Should().Be("Encoded in 1.5 s");
    }

    [Fact]
    public void SourceContext_IsCapturedFromTheLoggerCategory()
    {
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        logger.ForContext<InMemorySinkTests>().Warning("categorized");

        var entry = sink.Snapshot().Should().ContainSingle().Subject;
        entry.SourceContext.Should().Be(typeof(InMemorySinkTests).FullName);
        entry.ShortSource.Should().Be(nameof(InMemorySinkTests));
        entry.Level.Should().Be(LogEntryLevel.Warning);
    }

    [Fact]
    public void SourceContext_ThatIsNotAString_IsIgnored()
    {
        var sink = new InMemorySink();

        sink.Emit(Event("odd", LogEventLevel.Information, properties: [new LogEventProperty("SourceContext", new ScalarValue(42))]));

        sink.Snapshot()[0].SourceContext.Should().BeEmpty();
    }

    [Fact]
    public void Emit_Null_Throws()
    {
        var act = () => new InMemorySink().Emit(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static LogEvent Event(
        string message,
        LogEventLevel level,
        Exception? exception = null,
        DateTimeOffset? timestamp = null,
        IEnumerable<LogEventProperty>? properties = null) =>
        new(timestamp ?? DateTimeOffset.UtcNow, level, exception, new MessageTemplateParser().Parse(message), properties ?? []);
}
