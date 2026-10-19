// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Globalization;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Direction of text flow for the UI. Maps 1:1 to Avalonia's
/// <c>Avalonia.Media.FlowDirection</c> but stays in the portable Core assembly so view models
/// don't have to reference Avalonia.
/// </summary>
public enum TextFlowDirection
{
    LeftToRight,
    RightToLeft,
}

/// <summary>
/// Runtime localization service. Exposes translated strings via the <see cref="this[string]"/>
/// indexer so XAML can bind to <c>{Binding [MyKey], Source={StaticResource Localizer}}</c> and
/// react to culture changes without a restart.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>Currently active UI culture.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>Text flow direction implied by <see cref="CurrentCulture"/>.</summary>
    TextFlowDirection CurrentFlowDirection { get; }

    /// <summary>
    /// Cultures the app ships translations for. Settings UI uses this to populate the language
    /// picker.
    /// </summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Resolve the translation for <paramref name="key"/> in the current culture, falling back
    /// to the neutral culture if the key is missing.
    /// </summary>
    string this[string key] { get; }

    /// <summary>Fired after <see cref="CurrentCulture"/> changes and all bindings should refresh.</summary>
    event EventHandler? CultureChanged;

    /// <summary>
    /// Switch the UI culture. Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> for the indexer (so all
    /// bindings refresh) and <see cref="CultureChanged"/>.
    /// </summary>
    void SetCulture(CultureInfo culture);
}

public static class LocalizationServiceExtensions
{
    /// <summary>Format a localized template with the current UI culture's number/date rules.</summary>
    public static string Format(this ILocalizationService localization, string key, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return string.Format(localization.CurrentCulture, localization[key], args);
    }
}
