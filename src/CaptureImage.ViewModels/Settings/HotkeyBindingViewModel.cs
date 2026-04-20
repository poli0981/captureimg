using System;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Bridge between the <c>HotkeyRecorder</c> control and persistence. Owns the
/// <i>recording-mode</i> flag, the last validation outcome, and the
/// <i>conflict-detected</i> warning; handles the commit flow:
/// validate → sniff-for-conflict → persist → live-rebind the hotkey service
/// (so an armed capture picks up the new combo without disarm/re-arm).
/// </summary>
public sealed partial class HotkeyBindingViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly IHotkeyService _hotkeys;
    private readonly IHotkeyConflictSniffer _sniffer;
    private readonly ILogger<HotkeyBindingViewModel> _logger;
    private bool _disposed;

    /// <summary>Exposed so the <c>HotkeyRecorder</c> control can resolve localized strings.</summary>
    public ILocalizationService Localization { get; }

    [ObservableProperty]
    private HotkeyBinding _currentBinding;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private HotkeyValidationResult _lastValidationResult = HotkeyValidationResult.Ok;

    [ObservableProperty]
    private bool _conflictDetected;

    /// <summary>Human-readable current binding (e.g. <c>"Ctrl+Shift+F12"</c>).</summary>
    public string DisplayText => CurrentBinding.ToString();

    /// <summary><c>true</c> when <see cref="LastValidationResult"/> is non-Ok — drives UI error styling.</summary>
    public bool HasError => LastValidationResult != HotkeyValidationResult.Ok;

    /// <summary>Localized error string for <see cref="LastValidationResult"/>, or empty when Ok.</summary>
    public string ErrorMessage => LastValidationResult switch
    {
        HotkeyValidationResult.NoPrimaryKey     => Localization["Settings_HotkeyErrorNoPrimary"],
        HotkeyValidationResult.ModifierOnlyKey  => Localization["Settings_HotkeyErrorModifierOnly"],
        HotkeyValidationResult.RequiresModifier => Localization["Settings_HotkeyErrorNeedsModifier"],
        HotkeyValidationResult.ReservedByWindows => Localization["Settings_HotkeyErrorReserved"],
        _ => string.Empty,
    };

    public HotkeyBindingViewModel(
        ISettingsStore settings,
        IHotkeyService hotkeys,
        IHotkeyConflictSniffer sniffer,
        ILocalizationService localization,
        ILogger<HotkeyBindingViewModel> logger)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _sniffer = sniffer;
        _logger = logger;
        Localization = localization;

        _currentBinding = settings.Current.CaptureHotkey;
        _settings.Changed += OnSettingsChanged;

        // Localized ErrorMessage + ConflictWarning come from the indexer — push a fresh
        // PropertyChanged on culture switch so bound TextBlocks re-resolve strings in place.
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        var binding = _settings.Current.CaptureHotkey;
        if (binding != CurrentBinding)
        {
            CurrentBinding = binding;
        }
    }

    [RelayCommand]
    private void StartRecording()
    {
        _logger.LogDebug("HotkeyRecorder StartRecording invoked.");
        IsRecording = true;
        LastValidationResult = HotkeyValidationResult.Ok;
        ConflictDetected = false;
    }

    [RelayCommand]
    private void CancelRecording()
    {
        _logger.LogDebug("HotkeyRecorder CancelRecording invoked (was recording={WasRecording}).", IsRecording);
        IsRecording = false;
        LastValidationResult = HotkeyValidationResult.Ok;
        ConflictDetected = false;
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        _logger.LogDebug("HotkeyRecorder ResetToDefault invoked.");
        ApplyBinding(HotkeyBinding.Default);
    }

    /// <summary>
    /// Offered by the recorder control when the user presses a complete combination.
    /// Rejects silently when not in recording mode so stray key events outside the
    /// recorder do not leak into settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Flow: <see cref="HotkeyBinding.Validate"/> first. Invalid → keep recording
    /// so the user sees the error and can try again. Valid → sniff for cross-process
    /// conflicts (non-blocking; user keeps the combo), persist, and live-rebind.
    /// </para>
    /// </remarks>
    public void TryCommitRecorded(HotkeyBinding candidate)
    {
        if (!IsRecording) return;

        var validation = candidate.Validate();
        LastValidationResult = validation;

        if (validation != HotkeyValidationResult.Ok)
        {
            _logger.LogDebug(
                "Rejecting recorded hotkey {Binding}: {Reason}.", candidate, validation);
            return;
        }

        ConflictDetected = _sniffer.IsConflicted(candidate);
        if (ConflictDetected)
        {
            _logger.LogInformation(
                "Recorded hotkey {Binding} is already claimed by another process; saving anyway.",
                candidate);
        }

        ApplyBinding(candidate);
    }

    private void ApplyBinding(HotkeyBinding binding)
    {
        _settings.Update(s => s with { CaptureHotkey = binding });
        CurrentBinding = binding;
        IsRecording = false;
        LastValidationResult = HotkeyValidationResult.Ok;

        // Live rebind so a currently-armed capture picks the new combo up immediately.
        // When the service is stopped (disarmed), the next Arm will SetBinding from settings.
        if (_hotkeys.CurrentBinding is not null)
        {
            _hotkeys.SetBinding(binding);
            _logger.LogInformation("Live-rebound capture hotkey to {Binding}.", binding);
        }
    }

    partial void OnCurrentBindingChanged(HotkeyBinding value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnLastValidationResultChanged(HotkeyValidationResult value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }
}
