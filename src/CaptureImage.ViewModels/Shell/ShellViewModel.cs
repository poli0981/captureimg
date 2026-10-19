// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Pipeline;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CaptureImage.ViewModels.Shell;

/// <summary>
/// Main window and tray icon: title, armed state, tray tooltip and the tray menu commands.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly CaptureCoordinator _coordinator;
    private readonly ISettingsStore _settings;
    private readonly IShellService _shell;
    private readonly IAppLifetime _lifetime;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public ShellViewModel(
        CaptureCoordinator coordinator,
        ISettingsStore settings,
        IShellService shell,
        IAppLifetime lifetime,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        IMessenger messenger,
        AppInfo appInfo)
    {
        _coordinator = coordinator;
        _settings = settings;
        _shell = shell;
        _lifetime = lifetime;
        _dispatcher = dispatcher;
        Localization = localization;
        _messenger = messenger;
        AppInfo = appInfo;

        _coordinator.StateChanged += OnCoordinatorStateChanged;
        Localization.PropertyChanged += OnLocalizationChanged;
        Refresh();
    }

    public ILocalizationService Localization { get; }

    public AppInfo AppInfo { get; }

    public string Title => "CaptureImage";

    public string Subtitle => AppInfo.DisplayVersion;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _trayToolTip = "CaptureImage";

    public string ArmMenuText => IsArmed ? Localization["Tray_Disarm"] : Localization["Tray_Arm"];

    [RelayCommand]
    private void ShowWindow() => _messenger.Send(new NavigateMessage(null));

    [RelayCommand]
    private void Navigate(string? pageKey) => _messenger.Send(new NavigateMessage(pageKey));

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

    [RelayCommand]
    private async Task CaptureWindowAsync() => await _coordinator.CaptureAsync(CaptureMode.Window).ConfigureAwait(true);

    [RelayCommand]
    private async Task CaptureRegionAsync() => await _coordinator.CaptureAsync(CaptureMode.Region).ConfigureAwait(true);

    [RelayCommand]
    private async Task CaptureMonitorAsync() => await _coordinator.CaptureAsync(CaptureMode.Monitor).ConfigureAwait(true);

    [RelayCommand]
    private void OpenOutputFolder()
    {
        var folder = _settings.Current.Capture.OutputDirectory;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = AppPaths.DefaultOutputDirectory;
        }

        Directory.CreateDirectory(folder);
        _shell.Open(folder);
    }

    [RelayCommand]
    private void Exit() => _lifetime.Shutdown();

    partial void OnIsArmedChanged(bool value) => OnPropertyChanged(nameof(ArmMenuText));

    private void Refresh()
    {
        IsArmed = _coordinator.IsArmed;
        IsBusy = _coordinator.IsBusy;
        var state = _coordinator.Phase switch
        {
            CapturePhase.Countdown => Localization.Format("Toast_Countdown", _coordinator.CountdownRemaining),
            CapturePhase.Selecting => Localization["Status_Selecting"],
            CapturePhase.Capturing => Localization["Status_Capturing"],
            CapturePhase.Previewing => Localization["Preview_Prompt"],
            CapturePhase.Saving => Localization["Status_Saving"],
            _ when IsArmed => Localization["Status_Armed"],
            _ => Localization["Status_Idle"],
        };

        // Tray tooltips are limited to 127 characters.
        var tip = $"CaptureImage — {state}";
        TrayToolTip = tip.Length > 127 ? tip[..127] : tip;
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!_disposed)
        {
            Refresh();
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == "Item[]")
        {
            Refresh();
            OnPropertyChanged(nameof(ArmMenuText));
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
        Localization.PropertyChanged -= OnLocalizationChanged;
    }
}
