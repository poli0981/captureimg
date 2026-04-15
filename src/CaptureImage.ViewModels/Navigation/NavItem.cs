namespace CaptureImage.ViewModels.Navigation;

/// <summary>
/// Describes a single entry in the main navigation rail.
/// </summary>
/// <param name="Key">Stable identifier for the destination (used for logging + analytics).</param>
/// <param name="LabelKey">Localization resource key for the display label.</param>
/// <param name="IconGlyph">Fluent-style icon glyph or path data. M0 uses a short text placeholder.</param>
/// <param name="TargetViewModel">The VM type that should be activated when the user selects this item.</param>
public sealed record NavItem(
    string Key,
    string LabelKey,
    string IconGlyph,
    Type TargetViewModel);
