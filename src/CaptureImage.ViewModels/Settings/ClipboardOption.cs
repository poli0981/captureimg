namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Pickable entry for the Settings → Clipboard mode ComboBox. <see cref="Code"/> is the
/// raw value persisted to <c>AppSettings.Capture.ClipboardMode</c> (one of <c>None</c>,
/// <c>Copy</c>, <c>CopyAndSave</c>); the localized <see cref="DisplayLabel"/> is rebuilt
/// on culture change.
/// </summary>
public sealed record ClipboardOption(string Code, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}
