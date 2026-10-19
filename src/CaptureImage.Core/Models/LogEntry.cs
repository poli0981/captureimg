// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;

namespace CaptureImage.Core.Models;

/// <summary>One log event as shown in the in-app log viewer.</summary>
/// <param name="Timestamp">When the event was written.</param>
/// <param name="Level">Severity.</param>
/// <param name="SourceContext">Logger category (usually the class name).</param>
/// <param name="Message">Rendered message.</param>
/// <param name="Exception">Exception text, if any (truncated).</param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogEntryLevel Level,
    string SourceContext,
    string Message,
    string? Exception)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>Three-letter level code (<c>INF</c>, <c>WRN</c>…), as in the log file.</summary>
    public string LevelText => Level switch
    {
        LogEntryLevel.Verbose => "VRB",
        LogEntryLevel.Debug => "DBG",
        LogEntryLevel.Information => "INF",
        LogEntryLevel.Warning => "WRN",
        LogEntryLevel.Error => "ERR",
        _ => "FTL",
    };

    /// <summary>"Warning: message" — what screen readers announce for a log row.</summary>
    public override string ToString() => $"{Level}: {Message}";

    /// <summary>Class name without the namespace, for compact display.</summary>
    public string ShortSource
    {
        get
        {
            var dot = SourceContext.LastIndexOf('.');
            return dot >= 0 ? SourceContext[(dot + 1)..] : SourceContext;
        }
    }
}

/// <summary>Severity of a <see cref="LogEntry"/> (mirrors Serilog's levels).</summary>
public enum LogEntryLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}
