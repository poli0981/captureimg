using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace CaptureImage.App;

/// <summary>
/// Central Serilog configuration. M3 will swap the console sink for the in-memory ring buffer
/// sink that backs the real-time log viewer; M0 only needs file + console.
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
    /// Bootstrap logger used before DI is built. Writes to console + a rolling file.
    /// </summary>
    public static ILogger CreateBootstrapLogger()
    {
        var logPath = Path.Combine(GetLogsDir(), "captureimg-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.WithProperty("App", "CaptureImage")
            .WriteTo.Console()
            .WriteTo.Async(a => a.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: false,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
            .CreateLogger();
    }
}
