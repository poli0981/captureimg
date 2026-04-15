using System;

namespace CaptureImage.Core.Models;

/// <summary>
/// Single log event shown in the real-time log viewer. Stored in a bounded ring buffer
/// by the in-memory Serilog sink.
/// </summary>
/// <param name="Timestamp">When the event was emitted (UTC-local, no reformat).</param>
/// <param name="Level">Severity — mapped from Serilog's <c>LogEventLevel</c>.</param>
/// <param name="SourceContext">Fully-qualified logger name if present, else empty.</param>
/// <param name="Message">Rendered message text (format tokens already applied).</param>
/// <param name="Exception">Exception stringification if any, else null.</param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string SourceContext,
    string Message,
    string? Exception);

/// <summary>Levels the log viewer filters on. Mapped from Serilog's LogEventLevel.</summary>
public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}
