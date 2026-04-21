using CaptureImage.Core.Logging;
using CaptureImage.Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.App;

/// <summary>
/// Central Serilog configuration. Creates the in-memory ring buffer sink once, exposes it
/// so the DI container can hand it to the log viewer, and wires console + rolling file.
/// </summary>
internal static class LoggingSetup
{
    /// <summary>
    /// Returns the root directory under <c>%LocalAppData%\CaptureImage</c>, creating it if needed.
    /// </summary>
    public static string GetAppDataDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CaptureImage");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetLogsDir() => LogPaths.GetLogsDirectory();

    /// <summary>
    /// Build the Serilog root logger together with the in-memory sink and the runtime
    /// level switch, and assign the logger to <see cref="Log.Logger"/>. Both are returned
    /// so the DI container can register them — the sink drives the log viewer, the switch
    /// drives the Settings → Log Level picker.
    /// </summary>
    /// <remarks>
    /// The switch boots at <see cref="LogEventLevel.Debug"/> so early-startup telemetry is
    /// always captured; <c>Program.Main</c> narrows it to the user's configured value once
    /// <c>ISettingsStore.LoadAsync</c> has finished.
    /// </remarks>
    public static (InMemorySink Sink, LoggingLevelSwitch LevelSwitch) Initialize()
    {
        var logPath = Path.Combine(GetLogsDir(), "captureimg-.log");
        var sink = new InMemorySink();
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.WithProperty("App", "CaptureImage")
            .Enrich.With(new CallerPropertyDefaultsEnricher())
            .WriteTo.Console()
            .WriteTo.Sink(sink)
            .WriteTo.Async(a => a.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: false,
                // {File}:{Line} shows caller context injected by CallerAwareLoggerExtensions.
                // Missing on legacy call sites — Serilog prints a bare ":" then, so keep the
                // template consistent and accept that one cosmetic artifact until those are
                // migrated. {Properties:j} still JSON-dumps every non-templated property.
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] ({File}:{Line}) {Message:lj} {Properties:j}{NewLine}{Exception}"))
            .CreateLogger();

        return (sink, levelSwitch);
    }

    /// <summary>
    /// Map the persisted <c>LogLevel</c> string (one of <c>Debug</c> / <c>Information</c> /
    /// <c>Warning</c> / <c>Error</c>) to a <see cref="LogEventLevel"/>. Unknown strings
    /// fall back to <see cref="LogEventLevel.Information"/>.
    /// </summary>
    public static LogEventLevel ParseLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "debug"       => LogEventLevel.Debug,
        "information" => LogEventLevel.Information,
        "info"        => LogEventLevel.Information,
        "warning"     => LogEventLevel.Warning,
        "warn"        => LogEventLevel.Warning,
        "error"       => LogEventLevel.Error,
        _             => LogEventLevel.Information,
    };
}
