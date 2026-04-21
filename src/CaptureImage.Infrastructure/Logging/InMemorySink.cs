using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>
/// Serilog <see cref="ILogEventSink"/> that keeps the most recent N events in a bounded
/// ring buffer. The log viewer VM subscribes to <see cref="Emitted"/> to append live
/// entries, and can query <see cref="Snapshot"/> to hydrate on first show.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe: enqueue happens under a lock, snapshots return a fresh list so callers
/// can enumerate without holding the lock.
/// </para>
/// <para>
/// <see cref="Emitted"/> fires synchronously from the logging thread — subscribers must
/// marshal to the UI thread themselves.
/// </para>
/// </remarks>
public sealed class InMemorySink : ILogEventSink, ILogBufferSource
{
    public const int MaxCapacity = 2_000;

    private readonly object _gate = new();
    private readonly LinkedList<LogEntry> _buffer = new();
    private bool _paused;

    public event EventHandler<LogEntry>? Emitted;

    /// <summary>
    /// When <c>true</c>, new events are still added to the buffer but <see cref="Emitted"/>
    /// is not raised — the log viewer's "Pause" button flips this.
    /// </summary>
    public bool Paused
    {
        get { lock (_gate) return _paused; }
        set { lock (_gate) _paused = value; }
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = MapToEntry(logEvent);

        bool shouldRaise;
        lock (_gate)
        {
            _buffer.AddLast(entry);
            while (_buffer.Count > MaxCapacity)
            {
                _buffer.RemoveFirst();
            }
            shouldRaise = !_paused;
        }

        if (shouldRaise)
        {
            Emitted?.Invoke(this, entry);
        }
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return new List<LogEntry>(_buffer);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buffer.Clear();
        }
    }

    private static LogEntry MapToEntry(LogEvent ev)
    {
        string? sourceContext = null;
        if (ev.Properties.TryGetValue("SourceContext", out var sc) && sc is ScalarValue sv && sv.Value is string sctx)
        {
            sourceContext = sctx;
        }

        using var writer = new StringWriter();
        ev.RenderMessage(writer);

        return new LogEntry(
            Timestamp: ev.Timestamp,
            Level: MapLevel(ev.Level),
            SourceContext: sourceContext ?? string.Empty,
            Message: writer.ToString(),
            Exception: ev.Exception?.ToString())
        {
            // Caller-info is injected by CallerAwareLoggerExtensions via MEL scope; absent
            // on legacy call sites that still use Log.X or bare ILogger.LogInformation.
            File = ReadStringProperty(ev, "File"),
            Line = ReadInt32Property(ev, "Line"),
            Member = ReadStringProperty(ev, "Member"),
        };
    }

    private static string? ReadStringProperty(LogEvent ev, string name)
    {
        if (ev.Properties.TryGetValue(name, out var value) &&
            value is ScalarValue { Value: string s } &&
            !string.IsNullOrEmpty(s))
        {
            return s;
        }
        return null;
    }

    private static int? ReadInt32Property(LogEvent ev, string name)
    {
        if (ev.Properties.TryGetValue(name, out var value) &&
            value is ScalarValue { Value: int i } &&
            i > 0)
        {
            return i;
        }
        return null;
    }

    private static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose     => LogLevel.Verbose,
        LogEventLevel.Debug       => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning     => LogLevel.Warning,
        LogEventLevel.Error       => LogLevel.Error,
        LogEventLevel.Fatal       => LogLevel.Fatal,
        _                         => LogLevel.Information,
    };
}
