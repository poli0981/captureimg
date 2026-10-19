// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>In-app updates from GitHub Releases (Velopack).</summary>
public interface IUpdateService
{
    string CurrentVersion { get; }

    /// <summary>False when running from source (not a Velopack install).</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable progress lines for the Update page.</summary>
    event EventHandler<string>? LogEmitted;

    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);

    Task DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hand the downloaded update to the Velopack updater, which waits for this process to
    /// exit, applies it and restarts the app. The caller must then shut down normally.
    /// </summary>
    void ApplyOnExit();
}
