using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Dashboard tab. Shows the live list of capture targets and controls the arm/disarm/capture
/// flow. Process changes come from <see cref="IProcessWatcher"/>; capture requests go through
/// the <see cref="CaptureOrchestrator"/>; the hotkey trigger comes from <see cref="IHotkeyService"/>.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IProcessDetector _detector;
    private readonly IProcessWatcher _watcher;
    private readonly IHotkeyService _hotkeys;
    private readonly CaptureOrchestrator _orchestrator;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<DashboardViewModel> _logger;

    /// <summary>
    /// Debounce delay for process-lifecycle events — multiple start/stop events within this
    /// window collapse into a single refresh. Matches the WMI poll interval loosely.
    /// </summary>
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(500);

    private CancellationTokenSource? _pendingRefresh;
    private bool _disposed;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private GameTargetViewModel? _selectedTarget;

    [ObservableProperty]
    private string _statusMessage = "Idle — select a target and arm to begin.";

    [ObservableProperty]
    private string? _lastCapturePath;

    public ObservableCollection<GameTargetViewModel> Targets { get; } = new();

    public DashboardViewModel(
        IProcessDetector detector,
        IProcessWatcher watcher,
        IHotkeyService hotkeys,
        CaptureOrchestrator orchestrator,
        IUIThreadDispatcher dispatcher,
        ILogger<DashboardViewModel> logger)
    {
        _detector = detector;
        _watcher = watcher;
        _hotkeys = hotkeys;
        _orchestrator = orchestrator;
        _dispatcher = dispatcher;
        _logger = logger;

        _watcher.Changed += OnProcessChanged;
        _watcher.Start();

        _hotkeys.Triggered += OnHotkeyTriggered;

        // Fire the first refresh without blocking the constructor.
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed) return;

        try
        {
            IsLoading = true;
            var targets = await _detector.EnumerateTargetsAsync().ConfigureAwait(true);
            ApplyTargets(targets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate capture targets.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanArm))]
    private void Arm()
    {
        if (SelectedTarget is null) return;

        _hotkeys.SetBinding(HotkeyBinding.Default);
        IsArmed = true;
        StatusMessage = $"Armed: press {HotkeyBinding.Default} to capture {SelectedTarget.DisplayName}.";
        _logger.LogInformation("Armed capture for {Target} with hotkey {Hotkey}.",
            SelectedTarget.DisplayName, HotkeyBinding.Default);
    }

    [RelayCommand(CanExecute = nameof(CanDisarm))]
    private void Disarm()
    {
        _hotkeys.Stop();
        IsArmed = false;
        StatusMessage = "Disarmed.";
        _logger.LogInformation("Disarmed capture.");
    }

    private bool CanArm() => !IsArmed && !IsCapturing && SelectedTarget is not null;
    private bool CanDisarm() => IsArmed && !IsCapturing;

    partial void OnSelectedTargetChanged(GameTargetViewModel? value)
    {
        ArmCommand.NotifyCanExecuteChanged();
        DisarmCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsArmedChanged(bool value)
    {
        ArmCommand.NotifyCanExecuteChanged();
        DisarmCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCapturingChanged(bool value)
    {
        ArmCommand.NotifyCanExecuteChanged();
        DisarmCommand.NotifyCanExecuteChanged();
    }

    private void OnHotkeyTriggered(object? sender, EventArgs e)
    {
        // SharpHook fires from a background thread — marshal to UI before mutating state.
        _dispatcher.Post(async () => await CaptureOnceAsync().ConfigureAwait(true));
    }

    /// <summary>
    /// Execute one capture against the currently selected target using the default format
    /// (PNG) and the user's Pictures\CaptureImage folder. M3 will plug real settings in here.
    /// </summary>
    private async Task CaptureOnceAsync()
    {
        if (_disposed || SelectedTarget is null || IsCapturing) return;

        var target = SelectedTarget.Target;
        try
        {
            IsCapturing = true;
            StatusMessage = $"Capturing {target.DisplayName}…";

            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "CaptureImage");

            var request = new CaptureRequest(
                Target: target,
                Format: ImageFormat.Png,
                OutputDirectory: outputDir,
                FileNameTemplate: FileNameStrategy.DefaultTemplate,
                JpegQuality: 90,
                WebpQuality: 85);

            var result = await _orchestrator.ExecuteAsync(request).ConfigureAwait(true);

            switch (result)
            {
                case CaptureResult.Success ok:
                    LastCapturePath = ok.FilePath;
                    StatusMessage = $"Saved: {Path.GetFileName(ok.FilePath)} ({ok.Width}x{ok.Height}, {ok.Duration.TotalMilliseconds:F0} ms)";
                    break;
                case CaptureResult.Failure fail:
                    StatusMessage = $"Capture failed ({fail.ErrorCode}): {fail.DeveloperMessage}";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception in CaptureOnceAsync.");
            StatusMessage = "Capture crashed: " + ex.Message;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void ApplyTargets(IReadOnlyList<GameTarget> newTargets)
    {
        var previousPid = SelectedTarget?.ProcessId;

        Targets.Clear();
        foreach (var target in newTargets)
        {
            Targets.Add(new GameTargetViewModel(target));
        }

        if (previousPid is { } pid)
        {
            foreach (var vm in Targets)
            {
                if (vm.ProcessId == pid)
                {
                    SelectedTarget = vm;
                    break;
                }
            }
        }

        _logger.LogDebug("Dashboard reconciled to {Count} target(s).", Targets.Count);
    }

    private void OnProcessChanged(object? sender, ProcessChange e)
    {
        _pendingRefresh?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingRefresh = cts;
        var token = cts.Token;

        _dispatcher.Post(async () =>
        {
            try
            {
                await Task.Delay(RefreshDebounce, token).ConfigureAwait(true);
                if (token.IsCancellationRequested) return;
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer event — expected.
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeys.Triggered -= OnHotkeyTriggered;
        _watcher.Changed -= OnProcessChanged;
        _pendingRefresh?.Cancel();
        _pendingRefresh?.Dispose();
    }
}
