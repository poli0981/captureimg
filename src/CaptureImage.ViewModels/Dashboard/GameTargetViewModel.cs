using CaptureImage.Core.Models;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Thin view-model wrapper around <see cref="GameTarget"/>. Keeps the VM project free of
/// Avalonia — <see cref="IconBytes"/> is raw PNG, and the UI project converts it into
/// an <c>Avalonia.Media.Imaging.Bitmap</c> via a value converter.
/// </summary>
public sealed class GameTargetViewModel : ViewModelBase
{
    public GameTargetViewModel(GameTarget target)
    {
        Target = target;
    }

    public GameTarget Target { get; }

    public uint ProcessId => Target.ProcessId;

    public string DisplayName => Target.DisplayName;

    public string ProcessName => Target.ProcessName;

    public string ExecutablePath => Target.ExecutablePath;

    public byte[]? IconBytes => Target.IconBytes;

    public bool IsSteamGame => Target.IsSteamGame;

    public string? SteamAppName => Target.SteamInfo?.Name;

    public string SteamBadgeTooltip =>
        Target.SteamInfo is null
            ? string.Empty
            : $"Steam game: {Target.SteamInfo.Name} (AppId {Target.SteamInfo.AppId}). " +
              "F12 screenshot khả dụng qua Steam Overlay. Một số game có anti-cheat có thể chặn capture ngoài.";
}
