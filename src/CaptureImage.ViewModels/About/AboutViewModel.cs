using System.Reflection;

namespace CaptureImage.ViewModels.About;

/// <summary>
/// About tab. M4 will flesh this out with third-party attributions, legal, docs links.
/// M0 already shows the assembly version so we can verify the build is picking up
/// <c>Directory.Build.props</c> correctly.
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    public AboutViewModel()
    {
        var asm = typeof(AboutViewModel).Assembly;
        AppVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public string AppName => "CaptureImage";

    public string AppVersion { get; }

    public string License => "GPL-3.0-or-later";

    public string Placeholder => "About — third-party, legal, docs land in M4.";
}
