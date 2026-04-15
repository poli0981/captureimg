using System;
using System.IO;
using CaptureImage.Infrastructure.Logging;
using Serilog;
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

    public static string GetLogsDir()
    {
        var dir = Path.Combine(GetAppDataDir(), "logs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Build the Serilog root logger together with the in-memory sink, and assign it to
    /// <see cref="Log.Logger"/>. Returns the sink so the DI container can register it.
    /// </summary>
    public static InMemorySink Initialize()
    {
        var logPath = Path.Combine(GetLogsDir(), "captureimg-.log");
        var sink = new InMemorySink();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.WithProperty("App", "CaptureImage")
            .WriteTo.Console()
            .WriteTo.Sink(sink)
            .WriteTo.Async(a => a.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: false,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
            .CreateLogger();

        return sink;
    }
}
