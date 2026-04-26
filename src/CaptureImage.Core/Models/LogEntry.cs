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
/// <remarks>
/// The <see cref="File"/>, <see cref="Line"/>, and <see cref="Member"/> init-only properties are
/// populated when the call site used the <c>LogXAt</c> helpers from
/// <c>CaptureImage.Core.Logging.CallerAwareLoggerExtensions</c>; they stay null for legacy
/// call sites that still log via <c>ILogger&lt;T&gt;.LogInformation</c> or the static
/// <c>Serilog.Log</c> API.
/// </remarks>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string SourceContext,
    string Message,
    string? Exception)
{
    /// <summary>Source file name (e.g. <c>WindowsGraphicsCaptureEngine.cs</c>) — no directory.</summary>
    public string? File { get; init; }

    /// <summary>Line number in <see cref="File"/> where the log was emitted.</summary>
    public int? Line { get; init; }

    /// <summary>Method or property name that emitted the log.</summary>
    public string? Member { get; init; }

    /// <summary>
    /// Convenience string for the log viewer — <c>"File.cs:42"</c> when both are present,
    /// else empty. Computed; never participates in record equality.
    /// </summary>
    public string FileLineText =>
        !string.IsNullOrEmpty(File) && Line is > 0
            ? $"{File}:{Line}"
            : string.Empty;

    /// <summary>
    /// Pre-formatted timestamp for the log viewer column — <c>"HH:mm:ss.fff"</c>. Done as a
    /// computed property because WinUI 3 <c>{Binding}</c> doesn't support
    /// <c>StringFormat</c> the way Avalonia/WPF do; keeping the format in code-behind-free
    /// view XAML means one place (here) carries the format.
    /// </summary>
    public string TimestampText =>
        Timestamp.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
}

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
