namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Dashboard tab. M1 will populate this with the real process list, Steam warnings,
/// capture state, and the "Arm" button. M0 only needs a shell for navigation to land on.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    public DashboardViewModel()
    {
    }

    public string Placeholder => "Dashboard — process list will appear here in M1.";
}
