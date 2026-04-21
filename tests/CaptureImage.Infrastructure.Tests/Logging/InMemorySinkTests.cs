using System;
using System.Collections.Generic;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Logging;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Logging;

/// <summary>
/// Covers the ring-buffer semantics, pause/resume behaviour, and <c>Snapshot()</c> copy
/// guarantees of <see cref="InMemorySink"/> — the real-time log viewer's event source.
/// </summary>
public class InMemorySinkTests
{
    [Fact]
    public void Emit_AppendsToBuffer_AndRaisesEmitted_WhenNotPaused()
    {
        var sink = new InMemorySink();
        var raised = new List<LogEntry>();
        sink.Emitted += (_, entry) => raised.Add(entry);

        sink.Emit(Event("hello", LogEventLevel.Information));

        sink.Snapshot().Should().HaveCount(1);
        sink.Snapshot()[0].Message.Should().Be("hello");
        raised.Should().HaveCount(1);
        raised[0].Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void Emit_WhilePaused_StillBuffersButDoesNotRaiseEmitted()
    {
        var sink = new InMemorySink { Paused = true };
        var raised = new List<LogEntry>();
        sink.Emitted += (_, entry) => raised.Add(entry);

        sink.Emit(Event("hidden", LogEventLevel.Warning));

        sink.Snapshot().Should().HaveCount(1);
        raised.Should().BeEmpty();
    }

    [Fact]
    public void Emit_OverflowingCapacity_EvictsOldestFirst()
    {
        var sink = new InMemorySink();

        // Fire one beyond capacity so the first event has to be dropped.
        for (var i = 0; i < InMemorySink.MaxCapacity + 1; i++)
        {
            sink.Emit(Event($"msg-{i}", LogEventLevel.Information));
        }

        var snapshot = sink.Snapshot();
        snapshot.Should().HaveCount(InMemorySink.MaxCapacity);
        snapshot[0].Message.Should().Be("msg-1"); // index 0 evicted
        snapshot[^1].Message.Should().Be($"msg-{InMemorySink.MaxCapacity}");
    }

    [Fact]
    public void Snapshot_ReturnsCopy_MutationsDontAffectBuffer()
    {
        var sink = new InMemorySink();
        sink.Emit(Event("a", LogEventLevel.Information));

        var firstSnapshot = sink.Snapshot();
        sink.Emit(Event("b", LogEventLevel.Information));

        // The earlier snapshot is frozen at the moment it was taken.
        firstSnapshot.Should().HaveCount(1);
        firstSnapshot[0].Message.Should().Be("a");
        // But the sink itself has moved on.
        sink.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var sink = new InMemorySink();
        sink.Emit(Event("x", LogEventLevel.Information));
        sink.Emit(Event("y", LogEventLevel.Information));

        sink.Clear();

        sink.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Emit_CapturesCallerProperties_WhenPresentOnEvent()
    {
        var sink = new InMemorySink();
        var ev = EventWithCaller("with caller", "Engine.cs", 42, "Start");

        sink.Emit(ev);

        var entry = sink.Snapshot()[0];
        entry.File.Should().Be("Engine.cs");
        entry.Line.Should().Be(42);
        entry.Member.Should().Be("Start");
        entry.FileLineText.Should().Be("Engine.cs:42");
    }

    [Fact]
    public void Emit_LeavesCallerPropertiesNull_WhenAbsent()
    {
        var sink = new InMemorySink();
        sink.Emit(Event("plain", LogEventLevel.Information));

        var entry = sink.Snapshot()[0];
        entry.File.Should().BeNull();
        entry.Line.Should().BeNull();
        entry.Member.Should().BeNull();
        entry.FileLineText.Should().BeEmpty();
    }

    // -- helpers --------------------------------------------------------------

    private static LogEvent Event(string message, LogEventLevel level)
    {
        var template = new MessageTemplateParser().Parse(message);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception: null,
            template,
            properties: Array.Empty<LogEventProperty>());
    }

    private static LogEvent EventWithCaller(string message, string file, int line, string member)
    {
        var template = new MessageTemplateParser().Parse(message);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            template,
            properties: new[]
            {
                new LogEventProperty("File", new ScalarValue(file)),
                new LogEventProperty("Line", new ScalarValue(line)),
                new LogEventProperty("Member", new ScalarValue(member)),
            });
    }
}
