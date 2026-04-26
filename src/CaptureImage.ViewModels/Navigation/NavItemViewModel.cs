using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels.Navigation;

/// <summary>
/// A single entry in the main navigation rail. Exposes a <see cref="Label"/> that reads
/// through the localization service so culture changes refresh all nav items in place.
/// </summary>
public sealed partial class NavItemViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localization;

    public NavItemViewModel(
        string key,
        string labelKey,
        string iconGlyph,
        Type targetViewModel,
        ILocalizationService localization)
    {
        Key = key;
        LabelKey = labelKey;
        IconGlyph = iconGlyph;
        TargetViewModel = targetViewModel;
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationPropertyChanged;
    }

    /// <summary>Stable identifier for logging / telemetry — not localized.</summary>
    public string Key { get; }

    /// <summary>Resource key used to look up <see cref="Label"/>.</summary>
    public string LabelKey { get; }

    /// <summary>Fluent icon glyph or similar short identifier.</summary>
    public string IconGlyph { get; }

    /// <summary>The view model type activated when this nav item is selected.</summary>
    public Type TargetViewModel { get; }

    /// <summary>Localized display label. Refreshes automatically on culture changes.</summary>
    public string Label => _localization[LabelKey];

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // "Item[]" is the change notification for every indexer value at once.
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Label));
        }
    }

    public void Dispose()
    {
        _localization.PropertyChanged -= OnLocalizationPropertyChanged;
    }
}
