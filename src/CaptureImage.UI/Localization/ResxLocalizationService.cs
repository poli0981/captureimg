using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using CaptureImage.Core.Abstractions;

namespace CaptureImage.UI.Localization;

/// <summary>
/// <see cref="ILocalizationService"/> backed by the embedded <c>Strings.resx</c> files in
/// this assembly. Drives live culture switching: XAML binds to the indexer, and every
/// change raises <c>PropertyChanged("Item[]")</c> so all bindings refresh in place.
/// </summary>
public sealed class ResxLocalizationService : ILocalizationService
{
    private const string ResourceBaseName = "CaptureImage.UI.Resources.Strings.Strings";

    private static readonly CultureInfo[] ShippedCultures =
    {
        new("en-US"),
        new("vi-VN"),
        new("ar-SA"),
    };

    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public ResxLocalizationService()
    {
        _resourceManager = new ResourceManager(ResourceBaseName, typeof(ResxLocalizationService).Assembly);
        _currentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    public CultureInfo CurrentCulture => _currentCulture;

    public TextFlowDirection CurrentFlowDirection =>
        IsRightToLeft(_currentCulture) ? TextFlowDirection.RightToLeft : TextFlowDirection.LeftToRight;

    public IReadOnlyList<CultureInfo> SupportedCultures => ShippedCultures;

    /// <summary>
    /// Indexer lookup. Returns the key itself in square brackets if the lookup misses, so
    /// missing translations are obvious in the UI instead of rendering as empty strings.
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (_currentCulture.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // "Item[]" is the magic property name that tells Avalonia to refresh every indexer binding.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFlowDirection)));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsRightToLeft(CultureInfo culture)
    {
        // Language subtag based check — stable across regional variants (ar-SA, ar-EG, he-IL, fa-IR, ur-PK).
        var twoLetter = culture.TwoLetterISOLanguageName;
        return twoLetter is "ar" or "he" or "fa" or "ur";
    }
}
