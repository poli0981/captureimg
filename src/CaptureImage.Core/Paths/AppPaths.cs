// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Paths;

/// <summary>
/// Where CaptureImage keeps its own files. User data deliberately lives outside the Velopack
/// install folder (<c>%LocalAppData%\CaptureImage</c>): reinstalling with Setup.exe replaces
/// that folder wholesale, which used to wipe v1 settings and logs.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Environment variable that overrides the data directory (tests, smoke runs).</summary>
    public const string DataDirectoryVariable = "CAPTUREIMAGE_DATA_DIR";

    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    /// <summary><c>%LocalAppData%\poli0981\CaptureImage</c> unless overridden.</summary>
    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "poli0981", "CaptureImage");

    /// <summary>v1.x kept <c>settings.json</c> inside the Velopack install folder.</summary>
    public static string LegacySettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaptureImage", "settings.json");

    /// <summary>Default capture output folder: <c>Pictures\CaptureImage</c>.</summary>
    public static string DefaultOutputDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CaptureImage");

    public string DataDirectory { get; }

    /// <summary>True when the data directory was chosen explicitly (command line or environment).</summary>
    public bool IsOverridden { get; init; }

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string LibraryIndexFile => Path.Combine(DataDirectory, "library.json");

    public string ThumbnailCacheDirectory => Path.Combine(DataDirectory, "thumbnails");

    /// <summary>Create the data and logs folders if missing.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Resolve the data directory from (in order) <c>--data-dir &lt;path&gt;</c>, the
    /// <see cref="DataDirectoryVariable"/> environment variable, then the default.
    /// </summary>
    public static AppPaths FromCommandLine(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], "--data-dir", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return new AppPaths(args[i + 1]) { IsOverridden = true };
            }
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        return string.IsNullOrWhiteSpace(fromEnvironment)
            ? new AppPaths(DefaultDataDirectory)
            : new AppPaths(fromEnvironment) { IsOverridden = true };
    }
}
