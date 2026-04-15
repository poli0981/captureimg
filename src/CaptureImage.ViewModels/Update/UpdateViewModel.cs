using CaptureImage.Core.Abstractions;

namespace CaptureImage.ViewModels.Update;

/// <summary>
/// Update tab. M4 will wire this to <c>VelopackUpdateService</c> to show check/download/install
/// status, progress bar, and a log section. M3 gives it a localized title/placeholder.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    public ILocalizationService Localization { get; }

    public UpdateViewModel(ILocalizationService localization)
    {
        Localization = localization;
    }
}
