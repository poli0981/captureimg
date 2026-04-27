using System.Globalization;
using CaptureImage.UI.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.UI.Tests.Localization;

/// <summary>
/// Asserts that every load-bearing UI string is translated in all 3 shipped cultures
/// (en-US / vi-VN / ar-SA). The bracketed fallback (e.g. <c>"[Nav_Dashboard]"</c>) means
/// a key is missing from the resx — these tests catch that before it reaches the user.
/// </summary>
public class LocalizationCompletenessTests : IDisposable
{
    private readonly CultureInfo _savedUICulture;
    private readonly CultureInfo _savedCulture;
    private readonly CultureInfo? _savedDefaultUICulture;
    private readonly CultureInfo? _savedDefaultCulture;

    public LocalizationCompletenessTests()
    {
        _savedUICulture = CultureInfo.CurrentUICulture;
        _savedCulture = CultureInfo.CurrentCulture;
        _savedDefaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        _savedDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _savedUICulture;
        CultureInfo.CurrentCulture = _savedCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _savedDefaultUICulture;
        CultureInfo.DefaultThreadCurrentCulture = _savedDefaultCulture;
    }

    /// <summary>
    /// Curated list of keys that drive the v1.3 nav rail, page headers, and dialog
    /// buttons. Adding a new visible label? Append the key here so the matrix catches
    /// it if a translator misses one of the cultures.
    /// </summary>
    public static readonly string[] LoadBearingKeys =
    {
        // Nav rail
        "Nav_Dashboard", "Nav_Update", "Nav_Settings", "Nav_About", "Nav_LogViewer",
        // Page titles
        "Dashboard_Title", "Dashboard_Subtitle", "Dashboard_Refresh",
        "Dashboard_Arm", "Dashboard_Disarm", "Dashboard_StatusIdle",
        "Settings_Title", "Settings_Language", "Settings_Hotkey",
        "Settings_Theme", "Settings_Theme_System", "Settings_Theme_Light", "Settings_Theme_Dark",
        "Settings_Format", "Settings_OutputFolder", "Settings_Browse",
        "Settings_PreviewBeforeSave", "Settings_MinimizeToTray", "Settings_SoundEnabled",
        "Settings_Logging", "Settings_LogLevel",
        "About_Title", "About_Tagline", "About_VersionLabel", "About_LicenseLabel",
        "Update_Title", "Update_Check", "Update_Download", "Update_Install",
        // LogViewer drawer
        "Log_Title", "Log_Filter", "Log_Pause", "Log_Resume", "Log_Clear",
        "Log_RevealFolder", "Log_EmptyState",
        // Tray menu
        "Tray_ShowWindow", "Tray_OpenFolder", "Tray_Exit",
        // Toast headlines
        "Toast_CaptureSaved", "Toast_CaptureFailed", "Toast_Error",
        // Preview dialog
        "Preview_Title", "Preview_Prompt", "Preview_Save", "Preview_Discard",
    };

    public static IEnumerable<object[]> CultureMatrix()
    {
        foreach (var culture in new[] { "en-US", "vi-VN", "ar-SA" })
        {
            foreach (var key in LoadBearingKeys)
            {
                yield return new object[] { culture, key };
            }
        }
    }

    [Theory]
    [MemberData(nameof(CultureMatrix))]
    public void Key_HasNonBracketedTranslation(string cultureName, string key)
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        svc.SetCulture(CultureInfo.GetCultureInfo(cultureName));

        var value = svc[key];

        // Bracketed = missing key fallback. Empty = also a problem (nothing to render).
        value.Should().NotBe($"[{key}]",
            $"key '{key}' is missing from {cultureName} resx");
        value.Should().NotBeNullOrEmpty(
            $"key '{key}' renders empty in {cultureName} resx");
    }
}
