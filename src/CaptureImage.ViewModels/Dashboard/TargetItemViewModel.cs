// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>One capturable window in the Dashboard list. Updated in place on refresh.</summary>
public sealed partial class TargetItemViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private GameTarget _target;

    [ObservableProperty]
    private string? _profileName;

    [ObservableProperty]
    private bool _isLocked;

    public TargetItemViewModel(GameTarget target, ILocalizationService localization)
    {
        _target = target;
        _localization = localization;
    }

    public nint WindowHandle => Target.WindowHandle;

    public string DisplayName => Target.DisplayName;

    public string ProcessName => Target.ProcessName;

    public byte[]? IconBytes => Target.IconBytes;

    public bool IsMinimized => Target.IsMinimized;

    public bool IsSteamGame => Target.IsSteamGame;

    public bool HasProfile => ProfileName is not null;

    public string SteamTooltip => Target.SteamInfo is { } steam
        ? _localization.Format("Dashboard_SteamBadgeTooltip", steam.Name, steam.AppId)
        : string.Empty;

    /// <summary>Accessible name: title, process and badges in one sentence.</summary>
    public string AutomationName => string.Join(
        ", ",
        new[]
        {
            DisplayName,
            ProcessName,
            IsSteamGame ? _localization["Dashboard_SteamBadge"] : null,
            HasProfile ? _localization.Format("Dashboard_ProfileBadge", ProfileName) : null,
            IsMinimized ? _localization["Dashboard_Minimized"] : null,
            IsLocked ? _localization["Dashboard_Locked"] : null,
        }.Where(part => !string.IsNullOrEmpty(part)));

    public override string ToString() => DisplayName;

    /// <summary>Refresh localized text after a language switch.</summary>
    public void RefreshText()
    {
        OnPropertyChanged(nameof(SteamTooltip));
        OnPropertyChanged(nameof(AutomationName));
    }

    partial void OnTargetChanged(GameTarget value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(IconBytes));
        OnPropertyChanged(nameof(IsMinimized));
        OnPropertyChanged(nameof(IsSteamGame));
        RefreshText();
    }

    partial void OnProfileNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProfile));
        OnPropertyChanged(nameof(AutomationName));
    }

    partial void OnIsLockedChanged(bool value) => OnPropertyChanged(nameof(AutomationName));
}
