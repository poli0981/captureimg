// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// The hotkey of one capture mode in Settings: shows the binding, records a new one (the
/// capture hotkeys are released while recording so the combination reaches the recorder),
/// and rejects invalid, duplicate or already-taken combinations with a message.
/// </summary>
public sealed partial class HotkeyBindingViewModel(
    CaptureMode mode,
    ISettingsStore settings,
    CaptureCoordinator coordinator,
    IHotkeyService hotkeys,
    ILocalizationService localization) : ViewModelBase
{
    private readonly ISettingsStore _settings = settings;
    private readonly CaptureCoordinator _coordinator = coordinator;
    private readonly IHotkeyService _hotkeys = hotkeys;
    private IDisposable? _suspension;

    public ILocalizationService Localization { get; } = localization;

    public CaptureMode Mode { get; } = mode;

    public string ModeName => Localization[$"Mode_{Mode}"];

    public string Description => Localization[$"Settings_Hotkey{Mode}Description"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText), nameof(IsDefault))]
    public partial HotkeyBinding Binding { get; set; } = settings.Current.Hotkeys.Get(mode);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial bool IsRecording { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public string DisplayText => IsRecording
        ? Localization["Hotkey_PressKeys"]
        : Binding.IsUnbound ? Localization["Hotkey_None"] : Binding.ToString();

    public bool IsDefault => Binding == HotkeyBinding.DefaultFor(Mode);

    [RelayCommand]
    private void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        _suspension = _coordinator.SuspendHotkeys();
        IsRecording = true;
        SetMessage(string.Empty, isError: false);
    }

    [RelayCommand]
    private void CancelRecording() => StopRecording();

    [RelayCommand]
    private void Clear()
    {
        StopRecording();
        Commit(HotkeyBinding.Unbound);
        SetMessage(string.Empty, isError: false);
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        StopRecording();
        TryCommit(HotkeyBinding.DefaultFor(Mode));
    }

    /// <summary>
    /// The recorder saw a key press. Returns <c>true</c> when the combination was accepted
    /// (recording ends); modifier-only presses are ignored while the user is still typing.
    /// </summary>
    public bool Submit(HotkeyModifiers modifiers, uint virtualKey)
    {
        if (!IsRecording)
        {
            return false;
        }

        var candidate = new HotkeyBinding(modifiers, virtualKey);
        var validation = candidate.Validate();
        if (validation is HotkeyValidationResult.NoPrimaryKey or HotkeyValidationResult.ModifierOnlyKey)
        {
            return false;
        }

        if (validation != HotkeyValidationResult.Ok)
        {
            SetMessage(Localization[$"Hotkey_Validation_{validation}"], isError: true);
            return false;
        }

        if (!TryCommit(candidate))
        {
            return false;
        }

        StopRecording();
        return true;
    }

    /// <summary>Show the registration result while armed (conflicts appear after arming).</summary>
    public void ApplyRegistration(HotkeyRegistration? registration)
    {
        if (IsRecording)
        {
            return;
        }

        switch (registration?.Status)
        {
            case HotkeyRegistrationStatus.Conflict:
                SetMessage(Localization["Hotkey_StatusConflict"], isError: true);
                break;
            case HotkeyRegistrationStatus.Invalid:
                SetMessage(Localization["Hotkey_StatusInvalid"], isError: true);
                break;
            default:
                if (HasError)
                {
                    SetMessage(string.Empty, isError: false);
                }
                break;
        }
    }

    /// <summary>Settings changed elsewhere (import, reset).</summary>
    public void Reload()
    {
        if (!IsRecording)
        {
            Binding = _settings.Current.Hotkeys.Get(Mode);
        }
    }

    /// <summary>Refresh localized text after a language switch.</summary>
    public void RefreshText()
    {
        OnPropertyChanged(nameof(ModeName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(DisplayText));
    }

    private bool TryCommit(HotkeyBinding candidate)
    {
        var current = _settings.Current.Hotkeys;
        foreach (var other in Enum.GetValues<CaptureMode>())
        {
            if (other != Mode && current.Get(other) == candidate)
            {
                SetMessage(Localization.Format("Hotkey_UsedByMode", Localization[$"Mode_{other}"]), isError: true);
                return false;
            }
        }

        if (_hotkeys.IsTakenByAnotherApp(candidate))
        {
            SetMessage(Localization["Hotkey_TakenByOtherApp"], isError: true);
            return false;
        }

        Commit(candidate);
        SetMessage(string.Empty, isError: false);
        return true;
    }

    private void Commit(HotkeyBinding binding)
    {
        Binding = binding;
        _settings.Update(s => s with { Hotkeys = s.Hotkeys.With(Mode, binding) });
    }

    private void StopRecording()
    {
        IsRecording = false;
        var suspension = _suspension;
        _suspension = null;
        suspension?.Dispose();
    }

    private void SetMessage(string message, bool isError)
    {
        Message = message;
        HasError = isError && message.Length > 0;
    }
}
