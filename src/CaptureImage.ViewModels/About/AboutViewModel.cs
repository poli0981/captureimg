using System.Reflection;
using CaptureImage.Core.Abstractions;

namespace CaptureImage.ViewModels.About;

/// <summary>
/// About tab. M4 will flesh this out with third-party attributions, legal, docs links.
/// M3 already shows the assembly version, localized labels, and license.
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    public ILocalizationService Localization { get; }

    public AboutViewModel(ILocalizationService localization)
    {
        Localization = localization;

        var asm = typeof(AboutViewModel).Assembly;
        AppVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public string AppName => "CaptureImage";

    public string AppVersion { get; }

    public string License => "GPL-3.0-or-later";
}
