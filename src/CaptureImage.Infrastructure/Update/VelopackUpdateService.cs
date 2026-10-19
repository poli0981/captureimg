// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace CaptureImage.Infrastructure.Update;

/// <summary>
/// In-app updates from GitHub Releases through Velopack. When the running binary is not a
/// Velopack install (<c>dotnet run</c>, portable zip, tests) the service reports
/// <see cref="UpdateStatus.Unavailable"/>.
/// </summary>
/// <remarks>
/// Applying an update hands the package to Velopack's updater, which waits for this process
/// to exit; the app then shuts down normally, so settings are flushed, the tray icon is
/// removed and logs are closed (v1 called <c>ApplyUpdatesAndRestart</c>, which killed the
/// process without any of that).
/// </remarks>
public sealed partial class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/poli0981/captureimg";

    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;
        try
        {
            _manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            LogInitialized(_manager.CurrentVersion?.ToString() ?? "dev", _manager.IsInstalled);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogInitFailed(ex);
            _manager = null;
        }
    }

    public event EventHandler<string>? LogEmitted;

    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? "dev";

    public bool IsAvailable => _manager?.IsInstalled == true;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || _manager is null)
        {
            Emit("Updates are only available in the installed app (not when running from source or the portable zip).");
            return new UpdateCheckResult(UpdateStatus.Unavailable, CurrentVersion, null, null);
        }

        Emit($"Current version {CurrentVersion}. Checking GitHub Releases…");
        try
        {
            var info = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            _pending = info;
            if (info is null)
            {
                Emit("You are on the latest version.");
                return new UpdateCheckResult(UpdateStatus.UpToDate, CurrentVersion, null, null);
            }

            var target = info.TargetFullRelease;
            Emit($"Version {target.Version} is available ({target.Size / 1024 / 1024.0:0.0} MB).");
            return new UpdateCheckResult(UpdateStatus.UpdateAvailable, CurrentVersion, target.Version?.ToString(), target.NotesMarkdown);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            LogCheckFailed(ex);
            Emit($"Update check failed: {ex.Message}");
            return new UpdateCheckResult(UpdateStatus.Failed, CurrentVersion, null, null);
        }
    }

    public async Task DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pending is null)
        {
            throw new InvalidOperationException("Check for updates first.");
        }

        Emit("Downloading…");
        await _manager.DownloadUpdatesAsync(_pending, percent => progress?.Report(percent), cancellationToken).ConfigureAwait(false);
        Emit("Download complete. Restart to install.");
    }

    public void ApplyOnExit()
    {
        if (_manager is null || _pending is null)
        {
            throw new InvalidOperationException("Download the update first.");
        }

        LogApplying(_pending.TargetFullRelease.Version?.ToString() ?? "?");
        Emit("Installing — CaptureImage restarts in a moment…");
        _manager.WaitExitThenApplyUpdates(_pending.TargetFullRelease, silent: false, restart: true);
    }

    private void Emit(string line)
    {
        LogLine(line);
        LogEmitted?.Invoke(this, line);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Velopack ready (version {Version}, installed: {Installed}).")]
    private partial void LogInitialized(string version, bool installed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Velopack could not be initialized; updates are disabled.")]
    private partial void LogInitFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed.")]
    private partial void LogCheckFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying update {Version} after exit.")]
    private partial void LogApplying(string version);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updater: {Line}")]
    private partial void LogLine(string line);
}
