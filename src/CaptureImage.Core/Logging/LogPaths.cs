using System;
using System.IO;

namespace CaptureImage.Core.Logging;

/// <summary>
/// Canonical on-disk locations for log files. Shared between <c>LoggingSetup</c> (which
/// configures Serilog's rolling-file sink) and <c>LogViewerViewModel</c> (which opens the
/// folder when the user clicks "Reveal logs folder") so both agree on the path.
/// </summary>
public static class LogPaths
{
    /// <summary>
    /// Returns <c>%LocalAppData%\CaptureImage\logs</c>, creating the directory tree if
    /// missing. Uses <see cref="Environment.SpecialFolder.LocalApplicationData"/> so the
    /// path is per-user and roams with the profile.
    /// </summary>
    public static string GetLogsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CaptureImage",
            "logs");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
