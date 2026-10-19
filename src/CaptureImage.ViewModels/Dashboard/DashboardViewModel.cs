// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.ObjectModel;
using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Profiles;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>Status of one capture hotkey, as shown on the Dashboard.</summary>
/// <param name="Mode">Capture mode.</param>
/// <param name="ModeName">Localized mode name.</param>
/// <param name="Binding">Display form of the key combination (empty when unbound).</param>
/// <param name="StatusText">Localized registration status.</param>
/// <param name="IsProblem">The key is armed but could not be registered.</param>
public sealed record HotkeyStatusItem(CaptureMode Mode, string ModeName, string Binding, string StatusText, bool IsProblem);

/// <summary>
/// Dashboard: the window list (refreshed by WinEvent notifications only while the page is
/// visible, and updated in place so selection and scroll position survive), arming, target
/// locking and per-hotkey status.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly CaptureCoordinator _coordinator;
    private readonly ITargetCatalog _catalog;
    private readonly IWindowWatcher _watcher;
    private readonly ISettingsStore _settings;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly IMessenger _messenger;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly List<TargetItemViewModel> _all = [];
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _isPageActive;
    private bool _stale = true;
    private bool _disposed;

    public DashboardViewModel(
        CaptureCoordinator coordinator,
        ITargetCatalog catalog,
        IWindowWatcher watcher,
        ISettingsStore settings,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        IMessenger messenger,
        ILogger<DashboardViewModel> logger)
    {
        _coordinator = coordinator;
        _catalog = catalog;
        _watcher = watcher;
        _settings = settings;
        _dispatcher = dispatcher;
        Localization = localization;
        _messenger = messenger;
        _logger = logger;

        _coordinator.StateChanged += OnCoordinatorStateChanged;
        _watcher.WindowsChanged += OnWindowsChanged;
        _settings.Changed += OnSettingsChanged;
        Localization.PropertyChanged += OnLocalizationChanged;
        UpdateCoordinatorState();
    }

    public ILocalizationService Localization { get; }

    /// <summary>Windows matching <see cref="SearchText"/>.</summary>
    public ObservableCollection<TargetItemViewModel> Targets { get; } = [];

    public ObservableCollection<HotkeyStatusItem> Hotkeys { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureSelectedCommand), nameof(LockSelectedCommand), nameof(CreateProfileCommand))]
    private TargetItemViewModel? _selectedTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTargets))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _lockedTargetName;

    [ObservableProperty]
    private bool _hasHotkeyProblem;

    public bool HasNoTargets => !IsLoading && Targets.Count == 0;

    public string ArmButtonText => IsArmed ? Localization["Dashboard_Disarm"] : Localization["Dashboard_Arm"];

    /// <summary>Set by the view: list refreshes only run while the page is on screen.</summary>
    public bool IsPageActive
    {
        get => _isPageActive;
        set
        {
            _isPageActive = value;
            if (value && _stale)
            {
                _ = RefreshAsync();
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_disposed || !await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsLoading = _all.Count == 0;
            var targets = await _catalog.EnumerateAsync().ConfigureAwait(true);
            Apply(targets);
            _stale = false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            LogRefreshFailed(ex);
        }
        finally
        {
            IsLoading = false;
            _refreshGate.Release();
        }
    }

    [RelayCommand]
    private void ToggleArm()
    {
        if (_coordinator.IsArmed)
        {
            _coordinator.Disarm();
        }
        else
        {
            _coordinator.Arm();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CaptureSelectedAsync()
    {
        if (SelectedTarget is { } selected)
        {
            await _coordinator.CaptureAsync(CaptureMode.Window, selected.Target).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CaptureRegionAsync() => await _coordinator.CaptureAsync(CaptureMode.Region).ConfigureAwait(true);

    [RelayCommand]
    private async Task CaptureMonitorAsync() => await _coordinator.CaptureAsync(CaptureMode.Monitor).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void LockSelected()
    {
        if (SelectedTarget is { } selected)
        {
            _coordinator.LockTarget(selected.Target);
        }
    }

    [RelayCommand]
    private void Unlock() => _coordinator.LockTarget(null);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CreateProfile()
    {
        if (SelectedTarget is { } selected)
        {
            _messenger.Send(new CreateProfileMessage(selected.Target));
            _messenger.Send(new NavigateMessage(PageKeys.Profiles));
        }
    }

    private bool HasSelection() => SelectedTarget is not null;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnIsArmedChanged(bool value) => OnPropertyChanged(nameof(ArmButtonText));

    /// <summary>Merge a fresh enumeration into the existing item view models.</summary>
    private void Apply(IReadOnlyList<GameTarget> targets)
    {
        var byHandle = _all.ToDictionary(item => item.WindowHandle);
        var next = new List<TargetItemViewModel>(targets.Count);
        foreach (var target in targets)
        {
            if (byHandle.Remove(target.WindowHandle, out var existing))
            {
                if (existing.Target != target)
                {
                    existing.Target = target;
                }
                next.Add(existing);
            }
            else
            {
                next.Add(new TargetItemViewModel(target, Localization));
            }
        }

        _all.Clear();
        _all.AddRange(next);
        UpdateBadges();
        ApplyFilter();
        LogRefreshed(_all.Count);
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var visible = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(t => t.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || t.ProcessName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        // Remove what disappeared, then insert/move into the right order: the collection
        // is edited in place so the list keeps its scroll position and selection.
        for (var i = Targets.Count - 1; i >= 0; i--)
        {
            if (!visible.Contains(Targets[i]))
            {
                Targets.RemoveAt(i);
            }
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var current = Targets.IndexOf(visible[i]);
            if (current < 0)
            {
                Targets.Insert(i, visible[i]);
            }
            else if (current != i)
            {
                Targets.Move(current, i);
            }
        }

        OnPropertyChanged(nameof(HasNoTargets));
    }

    private void UpdateBadges()
    {
        var profiles = _settings.Current.Profiles;
        var locked = _coordinator.LockedTarget?.WindowHandle;
        foreach (var item in _all)
        {
            item.ProfileName = ProfileResolver.Resolve(profiles, item.Target)?.Name;
            item.IsLocked = locked == item.WindowHandle;
        }
    }

    private void UpdateCoordinatorState()
    {
        IsArmed = _coordinator.IsArmed;
        IsBusy = _coordinator.IsBusy;
        LockedTargetName = _coordinator.LockedTarget?.DisplayName;
        StatusText = BuildStatus();

        Hotkeys.Clear();
        var registrations = _coordinator.Registrations;
        var settings = _settings.Current.Hotkeys;
        foreach (var mode in Enum.GetValues<CaptureMode>())
        {
            var registration = registrations.FirstOrDefault(r => r.Mode == mode);
            var binding = registration?.Binding ?? settings.Get(mode);
            var status = registration?.Status;
            var statusText = status switch
            {
                HotkeyRegistrationStatus.Registered => Localization["Hotkey_StatusActive"],
                HotkeyRegistrationStatus.Conflict => Localization["Hotkey_StatusConflict"],
                HotkeyRegistrationStatus.Invalid => Localization["Hotkey_StatusInvalid"],
                _ when binding.IsUnbound => Localization["Hotkey_StatusUnbound"],
                _ => Localization["Hotkey_StatusInactive"],
            };
            Hotkeys.Add(new HotkeyStatusItem(
                mode,
                Localization[$"Mode_{mode}"],
                binding.IsUnbound ? Localization["Hotkey_None"] : binding.ToString(),
                statusText,
                status is HotkeyRegistrationStatus.Conflict or HotkeyRegistrationStatus.Invalid));
        }
        HasHotkeyProblem = Hotkeys.Any(h => h.IsProblem);
        UpdateBadges();
    }

    private string BuildStatus() => _coordinator.Phase switch
    {
        CapturePhase.Countdown => Localization.Format("Toast_Countdown", _coordinator.CountdownRemaining),
        CapturePhase.Selecting => Localization["Status_Selecting"],
        CapturePhase.Capturing => Localization["Status_Capturing"],
        CapturePhase.Previewing => Localization["Preview_Prompt"],
        CapturePhase.Saving => Localization["Status_Saving"],
        _ when _coordinator.IsArmed && _coordinator.LockedTarget is { } locked => Localization.Format("Status_ArmedLocked", locked.DisplayName),
        _ when _coordinator.IsArmed => Localization["Status_Armed"],
        _ => Localization["Status_Idle"],
    };

    private void OnCoordinatorStateChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!_disposed)
        {
            UpdateCoordinatorState();
        }
    });

    private void OnWindowsChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        _stale = true;
        if (!_disposed && _isPageActive)
        {
            _ = RefreshAsync();
        }
    });

    private void OnSettingsChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!_disposed)
        {
            UpdateCoordinatorState();
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != "Item[]")
        {
            return;
        }

        UpdateCoordinatorState();
        OnPropertyChanged(nameof(ArmButtonText));
        foreach (var item in _all)
        {
            item.RefreshText();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        _watcher.WindowsChanged -= OnWindowsChanged;
        _settings.Changed -= OnSettingsChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
        _refreshGate.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dashboard shows {Count} windows.")]
    private partial void LogRefreshed(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not list the open windows.")]
    private partial void LogRefreshFailed(Exception exception);
}
