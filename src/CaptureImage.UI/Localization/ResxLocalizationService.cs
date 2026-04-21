using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<ResxLocalizationService> _logger;

    /// <summary>
    /// Tracks <c>(culture, key)</c> pairs we've already logged a "missing string" warning for.
    /// Missing keys are usually the same handful repeated on every refresh; logging once per
    /// pair per process keeps the signal without flooding the rolling file.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _loggedMissingKeys = new();

    private CultureInfo _currentCulture;

    public ResxLocalizationService(ILogger<ResxLocalizationService> logger)
    {
        _resourceManager = new ResourceManager(ResourceBaseName, typeof(ResxLocalizationService).Assembly);
        _currentCulture = CultureInfo.GetCultureInfo("en-US");
        _logger = logger;
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
                var value = _resourceManager.GetString(key, _currentCulture);
                if (value is null)
                {
                    LogMissingKeyOnce(key);
                    return $"[{key}]";
                }
                return value;
            }
            catch (Exception ex)
            {
                LogMissingKeyOnce(key, ex);
                return $"[{key}]";
            }
        }
    }

    private void LogMissingKeyOnce(string key, Exception? exception = null)
    {
        var dedupKey = $"{_currentCulture.Name}|{key}";
        if (!_loggedMissingKeys.TryAdd(dedupKey, 0)) return;

        if (exception is null)
        {
            _logger.LogWarning(
                "Localization key '{Key}' missing for culture {Culture}; falling back to bracketed key.",
                key, _currentCulture.Name);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Localization lookup threw for key '{Key}' culture {Culture}; falling back to bracketed key.",
                key, _currentCulture.Name);
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

        // The indexer passes _currentCulture explicitly to ResourceManager so resx lookup
        // is correct regardless of thread culture — but date/number/currency formatting
        // elsewhere in the app uses the ambient CurrentCulture. Align both so a vi-VN
        // user sees Vietnamese dates and an ar-SA user sees Arabic-Indic digits wherever
        // implicit .ToString() lands. DefaultThread* sets the baseline for threads that
        // haven't overridden their own culture (e.g. background threads the user hasn't
        // touched yet); the UI thread gets both explicit assignments.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

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
