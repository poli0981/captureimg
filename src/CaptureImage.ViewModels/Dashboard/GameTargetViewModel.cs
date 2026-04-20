using System;
using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Thin view-model wrapper around <see cref="GameTarget"/>. Keeps the VM project free of
/// Avalonia — <see cref="IconBytes"/> is raw PNG, and the UI project converts it into
/// an <c>Avalonia.Media.Imaging.Bitmap</c> via a value converter.
/// </summary>
/// <remarks>
/// Holds a reference to <see cref="ILocalizationService"/> so <see cref="SteamBadgeTooltip"/>
/// can refresh on culture changes without reallocating the target row. Disposed indirectly —
/// each target row lives and dies with the <c>ObservableCollection</c> in the dashboard,
/// and the next reconciliation pass replaces it.
/// </remarks>
public sealed class GameTargetViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private bool _disposed;

    public GameTargetViewModel(GameTarget target, ILocalizationService localization)
    {
        Target = target;
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationPropertyChanged;
    }

    public GameTarget Target { get; }

    public uint ProcessId => Target.ProcessId;

    public string DisplayName => Target.DisplayName;

    public string ProcessName => Target.ProcessName;

    public string ExecutablePath => Target.ExecutablePath;

    public byte[]? IconBytes => Target.IconBytes;

    public bool IsSteamGame => Target.IsSteamGame;

    public string? SteamAppName => Target.SteamInfo?.Name;

    /// <summary>
    /// Tooltip shown on the Steam badge. Localized via
    /// <c>Dashboard_SteamBadgeTooltip</c> with <c>{0}=AppName</c> and <c>{1}=AppId</c>;
    /// recomputes on culture changes.
    /// </summary>
    public string SteamBadgeTooltip
    {
        get
        {
            if (Target.SteamInfo is null) return string.Empty;
            return string.Format(
                _localization["Dashboard_SteamBadgeTooltip"],
                Target.SteamInfo.Name,
                Target.SteamInfo.AppId);
        }
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(SteamBadgeTooltip));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localization.PropertyChanged -= OnLocalizationPropertyChanged;
    }
}
