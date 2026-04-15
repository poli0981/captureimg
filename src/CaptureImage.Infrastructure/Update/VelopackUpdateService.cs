using System;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace CaptureImage.Infrastructure.Update;

/// <summary>
/// <see cref="IUpdateService"/> backed by <see cref="Velopack.UpdateManager"/> pointed at a
/// GitHub Releases repository. When the executing binary is not a Velopack-installed app
/// (e.g. <c>dotnet run</c> during development), the service reports
/// <see cref="UpdateStatus.Unavailable"/> and disables itself gracefully.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    /// <summary>
    /// Default GitHub repository URL the updater points at. Can be overridden via
    /// settings in a future milestone.
    /// </summary>
    private const string DefaultRepositoryUrl = "https://github.com/poli0981/captureimg";

    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;

        try
        {
            var source = new GithubSource(
                repoUrl: DefaultRepositoryUrl,
                accessToken: null,
                prerelease: false);

            _manager = new UpdateManager(source);
            _logger.LogInformation(
                "Velopack update service initialized: current={Version}, installed={IsInstalled}, repo={Repo}",
                _manager.CurrentVersion, _manager.IsInstalled, DefaultRepositoryUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Velopack UpdateManager could not be constructed; updates disabled.");
            _manager = null;
        }
    }

    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? "0.0.0-dev";

    public bool IsAvailable => _manager?.IsInstalled == true;

    public event EventHandler<string>? LogEmitted;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        EmitLog($"Current version: {CurrentVersion}");

        if (!IsAvailable || _manager is null)
        {
            EmitLog("Update service is unavailable (not a Velopack-installed binary). Running from source?");
            return new UpdateCheckResult(
                Status: UpdateStatus.Unavailable,
                CurrentVersion: CurrentVersion,
                AvailableVersion: null,
                ReleaseNotes: null);
        }

        EmitLog("Checking GitHub for updates…");
        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                EmitLog("No update available. You are on the latest release.");
                _pendingUpdate = null;
                return new UpdateCheckResult(
                    Status: UpdateStatus.UpToDate,
                    CurrentVersion: CurrentVersion,
                    AvailableVersion: null,
                    ReleaseNotes: null);
            }

            _pendingUpdate = info;
            var target = info.TargetFullRelease;
            EmitLog($"Update available: {target.Version} ({target.FileName}, {target.Size / 1024} KB)");

            return new UpdateCheckResult(
                Status: UpdateStatus.UpdateAvailable,
                CurrentVersion: CurrentVersion,
                AvailableVersion: target.Version?.ToString(),
                ReleaseNotes: target.NotesMarkdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed.");
            EmitLog($"Check failed: {ex.Message}");
            return new UpdateCheckResult(
                Status: UpdateStatus.Failed,
                CurrentVersion: CurrentVersion,
                AvailableVersion: null,
                ReleaseNotes: null);
        }
    }

    public async Task DownloadAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pendingUpdate is null)
        {
            throw new InvalidOperationException(
                "No pending update to download. Call CheckAsync first and ensure it returned UpdateAvailable.");
        }

        EmitLog("Downloading update…");
        try
        {
            await _manager.DownloadUpdatesAsync(
                _pendingUpdate,
                percent =>
                {
                    progress?.Report(percent);
                    if (percent is 25 or 50 or 75 or 100)
                    {
                        EmitLog($"Download progress: {percent}%");
                    }
                },
                ignoreDeltas: false,
                cancelToken: cancellationToken).ConfigureAwait(false);

            EmitLog("Download complete. Ready to install.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed.");
            EmitLog($"Download failed: {ex.Message}");
            throw;
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is null || _pendingUpdate is null)
        {
            throw new InvalidOperationException(
                "No pending update to install. Call CheckAsync + DownloadAsync first.");
        }

        EmitLog("Applying update and restarting…");
        _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    private void EmitLog(string line)
    {
        _logger.LogInformation("{Line}", line);
        LogEmitted?.Invoke(this, line);
    }
}
