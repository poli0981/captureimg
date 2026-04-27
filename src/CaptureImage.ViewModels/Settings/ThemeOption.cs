namespace CaptureImage.ViewModels.Settings;

public sealed record ThemeOption(string Code, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}
