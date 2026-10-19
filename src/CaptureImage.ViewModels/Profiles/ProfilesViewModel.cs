// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Profiles;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CaptureImage.ViewModels.Profiles;

/// <summary>One game profile in the list.</summary>
public sealed partial class ProfileItemViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(MatchSummary), nameof(Enabled), nameof(AutoArm))]
    private GameProfile _profile;

    public ProfileItemViewModel(GameProfile profile)
    {
        _profile = profile;
    }

    public string Id => Profile.Id;

    public string Name => !string.IsNullOrWhiteSpace(Profile.Name)
        ? Profile.Name
        : Profile.ExecutableName ?? Path.GetFileName(Profile.ExecutablePath) ?? "—";

    /// <summary>What the profile matches, most specific first.</summary>
    public string MatchSummary =>
        !string.IsNullOrWhiteSpace(Profile.ExecutablePath) ? Profile.ExecutablePath
        : !string.IsNullOrWhiteSpace(Profile.ExecutableName) ? Profile.ExecutableName
        : Profile.SteamAppId is { } appId ? "Steam " + appId.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public bool Enabled => Profile.Enabled;

    public bool AutoArm => Profile.AutoArm;

    public override string ToString() => Name;
}

/// <summary>
/// Profiles page: per-game output folder, format, file name template and HDR overrides,
/// plus auto-arm. Edits are written to settings as they are made.
/// </summary>
public sealed partial class ProfilesViewModel : ViewModelBase, IRecipient<CreateProfileMessage>, IDisposable
{
    private const string ProgramsFilter = "Programs (*.exe)|*.exe";

    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly IMessenger _messenger;
    private IReadOnlyList<GameProfile>? _lastWritten;
    private bool _loading;
    private bool _disposed;

    public ProfilesViewModel(
        ISettingsStore settings,
        IDialogService dialogs,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        IMessenger messenger)
    {
        _settings = settings;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        Localization = localization;
        _messenger = messenger;

        FormatOptions =
        [
            LocalizedOption.Create(string.Empty, localization, "Profiles_UseGlobal"),
            .. ImageFormatExtensions.UserSelectable.Select(f => LocalizedOption.Fixed(f.ToString(), f.Extension().ToUpperInvariant())),
        ];
        HdrOptions =
        [
            LocalizedOption.Create(string.Empty, localization, "Profiles_UseGlobal"),
            LocalizedOption.Create("Auto", localization, "Settings_HdrAuto"),
            LocalizedOption.Create("Off", localization, "Settings_HdrOff"),
        ];

        Reload();
        _settings.Changed += OnSettingsChanged;
        Localization.PropertyChanged += OnLocalizationChanged;
        _messenger.Register(this);
    }

    public ILocalizationService Localization { get; }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public IReadOnlyList<LocalizedOption<string>> FormatOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> HdrOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(BrowseExecutableCommand), nameof(BrowseOutputFolderCommand), nameof(ClearOutputFolderCommand))]
    private ProfileItemViewModel? _selectedProfile;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchesNothing))]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchesNothing))]
    private string _executableName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchesNothing), nameof(SteamAppIdInvalid))]
    private string _steamAppId = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _format = string.Empty;

    [ObservableProperty]
    private string _fileNameTemplate = string.Empty;

    [ObservableProperty]
    private string _hdrMode = string.Empty;

    [ObservableProperty]
    private bool _autoArm;

    [ObservableProperty]
    private bool _lockTarget = true;

    public bool HasSelection => SelectedProfile is not null;

    public bool HasProfiles => Profiles.Count > 0;

    /// <summary>The profile has no executable path, name or AppId, so it never applies.</summary>
    public bool MatchesNothing => HasSelection
        && string.IsNullOrWhiteSpace(ExecutablePath)
        && string.IsNullOrWhiteSpace(ExecutableName)
        && ParseAppId(SteamAppId) is null;

    public bool SteamAppIdInvalid => !string.IsNullOrWhiteSpace(SteamAppId) && ParseAppId(SteamAppId) is null;

    [RelayCommand]
    private void Add() => AddProfile(new GameProfile { Name = Localization["Profiles_NewProfile"] });

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var item = SelectedProfile!;
        var confirmed = await _dialogs.ConfirmAsync(
            Localization["Profiles_DeleteTitle"],
            Localization.Format("Profiles_DeleteMessage", item.Name),
            Localization["Profiles_Delete"],
            Localization["Common_Cancel"]).ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var index = Profiles.IndexOf(item);
        Profiles.Remove(item);
        Persist();
        SelectedProfile = Profiles.Count == 0 ? null : Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        OnPropertyChanged(nameof(HasProfiles));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task BrowseExecutableAsync()
    {
        var path = await _dialogs.PickFileAsync(Localization["Profiles_PickExecutable"], ProgramsFilter).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ExecutablePath = path;
        ExecutableName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(Name) || Name == Localization["Profiles_NewProfile"])
        {
            Name = Path.GetFileNameWithoutExtension(path);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync(Localization["Profiles_OutputFolder"], string.IsNullOrWhiteSpace(OutputDirectory) ? null : OutputDirectory).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputDirectory = folder;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ClearOutputFolder() => OutputDirectory = string.Empty;

    /// <summary>"Create profile" on the Dashboard: select the matching profile or start a new one.</summary>
    public void Receive(CreateProfileMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var target = message.Target;
        var existing = ProfileResolver.Resolve([.. Profiles.Select(p => p.Profile)], target);
        if (existing is not null)
        {
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == existing.Id);
            return;
        }

        var exeName = !string.IsNullOrEmpty(target.ExecutablePath)
            ? Path.GetFileName(target.ExecutablePath)
            : target.ProcessName + ".exe";
        AddProfile(new GameProfile
        {
            Name = target.SteamInfo?.Name ?? target.DisplayName,
            ExecutablePath = string.IsNullOrEmpty(target.ExecutablePath) ? null : target.ExecutablePath,
            ExecutableName = exeName,
            SteamAppId = target.SteamInfo?.AppId,
        });
    }

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        _loading = true;
        try
        {
            var p = value?.Profile;
            Name = p?.Name ?? string.Empty;
            Enabled = p?.Enabled ?? true;
            ExecutablePath = p?.ExecutablePath ?? string.Empty;
            ExecutableName = p?.ExecutableName ?? string.Empty;
            SteamAppId = p?.SteamAppId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            OutputDirectory = p?.OutputDirectory ?? string.Empty;
            Format = p?.Format?.ToString() ?? string.Empty;
            FileNameTemplate = p?.FileNameTemplate ?? string.Empty;
            HdrMode = p?.HdrMode ?? string.Empty;
            AutoArm = p?.AutoArm ?? false;
            LockTarget = p?.LockTarget ?? true;
        }
        finally
        {
            _loading = false;
        }
        OnPropertyChanged(nameof(MatchesNothing));
    }

    partial void OnNameChanged(string value) => Edit(p => p with { Name = value.Trim() });

    partial void OnEnabledChanged(bool value) => Edit(p => p with { Enabled = value });

    partial void OnExecutablePathChanged(string value) => Edit(p => p with { ExecutablePath = NullIfBlank(value) });

    partial void OnExecutableNameChanged(string value) => Edit(p => p with { ExecutableName = NullIfBlank(value) });

    partial void OnSteamAppIdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || ParseAppId(value) is not null)
        {
            Edit(p => p with { SteamAppId = ParseAppId(value) });
        }
    }

    partial void OnOutputDirectoryChanged(string value) => Edit(p => p with { OutputDirectory = NullIfBlank(value) });

    partial void OnFormatChanged(string value) =>
        Edit(p => p with { Format = Enum.TryParse<ImageFormat>(value, out var f) && f != ImageFormat.Jxr ? f : null });

    partial void OnFileNameTemplateChanged(string value) => Edit(p => p with { FileNameTemplate = NullIfBlank(value) });

    partial void OnHdrModeChanged(string value) => Edit(p => p with { HdrMode = NullIfBlank(value) });

    partial void OnAutoArmChanged(bool value) => Edit(p => p with { AutoArm = value });

    partial void OnLockTargetChanged(bool value) => Edit(p => p with { LockTarget = value });

    private void AddProfile(GameProfile profile)
    {
        var item = new ProfileItemViewModel(profile);
        Profiles.Add(item);
        Persist();
        SelectedProfile = item;
        OnPropertyChanged(nameof(HasProfiles));
    }

    private void Edit(Func<GameProfile, GameProfile> change)
    {
        if (_loading || SelectedProfile is not { } item)
        {
            return;
        }

        item.Profile = change(item.Profile);
        Persist();
    }

    private void Persist()
    {
        var profiles = Profiles.Select(p => p.Profile).ToList();
        _lastWritten = profiles;
        _settings.Update(s => s with { Profiles = profiles });
    }

    private void Reload()
    {
        var selectedId = SelectedProfile?.Id;
        var profiles = _settings.Current.Profiles;
        _lastWritten = profiles;
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(new ProfileItemViewModel(profile));
        }
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == selectedId) ?? Profiles.FirstOrDefault();
        OnPropertyChanged(nameof(HasProfiles));
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!_disposed && !ReferenceEquals(_settings.Current.Profiles, _lastWritten))
        {
            Reload();
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == "Item[]")
        {
            LocalizedOption.RefreshAll(FormatOptions);
            LocalizedOption.RefreshAll(HdrOptions);
        }
    }

    private static uint? ParseAppId(string? value) =>
        uint.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
        _messenger.UnregisterAll(this);
    }
}
