using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Dashboard tab. Shows the live list of capture targets driven by
/// <see cref="IProcessDetector"/> and <see cref="IProcessWatcher"/>.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IProcessDetector _detector;
    private readonly IProcessWatcher _watcher;
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
    private GameTargetViewModel? _selectedTarget;

    public ObservableCollection<GameTargetViewModel> Targets { get; } = new();

    public DashboardViewModel(
        IProcessDetector detector,
        IProcessWatcher watcher,
        IUIThreadDispatcher dispatcher,
        ILogger<DashboardViewModel> logger)
    {
        _detector = detector;
        _watcher = watcher;
        _dispatcher = dispatcher;
        _logger = logger;

        _watcher.Changed += OnProcessChanged;
        _watcher.Start();

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

    private void ApplyTargets(IReadOnlyList<GameTarget> newTargets)
    {
        // Simple reconcile: clear + repopulate. The list is short (<100 items typically);
        // fancy diffing is premature.
        var previousPid = SelectedTarget?.ProcessId;

        Targets.Clear();
        foreach (var target in newTargets)
        {
            Targets.Add(new GameTargetViewModel(target));
        }

        // Best-effort: restore selection across refreshes.
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
        // Coalesce bursts: cancel any pending refresh, schedule a new one.
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
        _watcher.Changed -= OnProcessChanged;
        _pendingRefresh?.Cancel();
        _pendingRefresh?.Dispose();
    }
}
