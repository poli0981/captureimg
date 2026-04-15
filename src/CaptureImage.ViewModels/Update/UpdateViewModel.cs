namespace CaptureImage.ViewModels.Update;

/// <summary>
/// Update tab. M4 will wire this to <c>VelopackUpdateService</c> to show check/download/install
/// status, progress bar, and a log section. M0 is a placeholder.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    public UpdateViewModel()
    {
    }

    public string Placeholder => "Update — Velopack integration lands in M4.";
}
