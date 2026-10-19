// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>
/// Ring buffer of recent log events for the in-app log viewer. Bounded by count and by
/// exception text length, so a burst of stack traces can't grow memory without limit.
/// Wrap it in <c>WriteTo.Async</c> so rendering never runs on the logging thread.
/// </summary>
public sealed class InMemorySink : ILogEventSink, ILogBufferSource
{
    public const int MaxCapacity = 2_000;
    private const int MaxExceptionLength = 8_192;

    // ":lj" renders strings without quotes, like the log file does.
    private static readonly MessageTemplateTextFormatter MessageFormatter = new("{Message:lj}", CultureInfo.InvariantCulture);

    private readonly Lock _gate = new();
    private readonly Queue<LogEntry> _buffer = new();
    private bool _paused;

    public event EventHandler<LogEntry>? Emitted;

    public bool Paused
    {
        get { lock (_gate) return _paused; }
        set { lock (_gate) _paused = value; }
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var entry = Map(logEvent);
        bool raise;
        lock (_gate)
        {
            _buffer.Enqueue(entry);
            while (_buffer.Count > MaxCapacity)
            {
                _buffer.Dequeue();
            }
            raise = !_paused;
        }

        if (raise)
        {
            Emitted?.Invoke(this, entry);
        }
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _buffer];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buffer.Clear();
        }
    }

    private static LogEntry Map(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string s } ? s : string.Empty;
        var exception = logEvent.Exception?.ToString();
        if (exception is { Length: > MaxExceptionLength })
        {
            exception = exception[..MaxExceptionLength] + Environment.NewLine + "…";
        }

        return new LogEntry(
            logEvent.Timestamp,
            Map(logEvent.Level),
            source,
            RenderMessage(logEvent),
            exception);
    }

    private static string RenderMessage(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        MessageFormatter.Format(logEvent, writer);
        return writer.ToString();
    }

    private static LogEntryLevel Map(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogEntryLevel.Verbose,
        LogEventLevel.Debug => LogEntryLevel.Debug,
        LogEventLevel.Warning => LogEntryLevel.Warning,
        LogEventLevel.Error => LogEntryLevel.Error,
        LogEventLevel.Fatal => LogEntryLevel.Fatal,
        _ => LogEntryLevel.Information,
    };
}
