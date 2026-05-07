namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Pickable entry for the Settings → Capture countdown ComboBox. <see cref="Seconds"/>
/// is what gets persisted into <c>AppSettings.Capture.CountdownSeconds</c>; the localized
/// <see cref="DisplayLabel"/> is rebuilt on culture change.
/// </summary>
public sealed record CountdownOption(int Seconds, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}
