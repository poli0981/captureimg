using System;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Self-update for CaptureImage. Backed by Velopack + GitHub Releases in production, or a
/// no-op stub in dev builds / when Velopack was never set up for the running installation.
/// </summary>
public interface IUpdateService
{
    /// <summary>Currently-installed version string (e.g. <c>"0.1.0"</c>).</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// True when the service can actually perform updates (i.e. Velopack metadata is present
    /// next to the executable). Dev / <c>dotnet run</c> sessions return false.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Raised whenever a log line should be appended to the Update tab's status log.
    /// Fires on whatever thread the update workflow is running on — subscribers marshal
    /// to the UI themselves.
    /// </summary>
    event EventHandler<string>? LogEmitted;

    /// <summary>
    /// Hit the update source and report whether a newer version is available.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Download the update that was returned by the most recent <see cref="CheckAsync"/>.
    /// Reports progress 0-100.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No pending update to download (caller skipped <see cref="CheckAsync"/> or the previous
    /// check returned <see cref="UpdateStatus.UpToDate"/>).
    /// </exception>
    Task DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply the downloaded update and restart the app. Does not return on success; throws
    /// if no download has been performed yet.
    /// </summary>
    void ApplyAndRestart();
}
