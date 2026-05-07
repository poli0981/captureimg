namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Pickable entry for the Settings → Capture mode ComboBox. <see cref="Code"/> is
/// the raw value persisted to <c>AppSettings.Capture.Mode</c> (one of <c>Window</c>,
/// <c>Region</c>); the localized <see cref="DisplayLabel"/> rebuilds on culture switch.
/// </summary>
public sealed record CaptureModeOption(string Code, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}
