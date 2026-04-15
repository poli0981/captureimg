namespace CaptureImage.Core.Models;

/// <summary>
/// High-level phase of the self-update workflow, shown in the Update tab UI.
/// </summary>
public enum UpdateStatus
{
    /// <summary>No check has been performed yet in this session.</summary>
    Idle,

    /// <summary>Currently talking to the GitHub API to see if a new release exists.</summary>
    Checking,

    /// <summary>Check completed: the installed version is already the newest.</summary>
    UpToDate,

    /// <summary>Check completed: a newer version is available for download.</summary>
    UpdateAvailable,

    /// <summary>Downloading the update package from the release CDN.</summary>
    Downloading,

    /// <summary>Download finished; package is ready to install.</summary>
    Ready,

    /// <summary>Installer has been triggered; the app is about to restart.</summary>
    Installing,

    /// <summary>Last operation produced an error — see the log section for details.</summary>
    Failed,

    /// <summary>App is running in a dev / self-built context where Velopack is not available.</summary>
    Unavailable,
}
