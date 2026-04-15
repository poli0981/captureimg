namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Settings tab. M3 will wire language picker, hotkey rebinder, capture options, import/export.
/// M0 is a placeholder.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
    }

    public string Placeholder => "Settings — language, hotkeys, capture options land in M3.";
}
