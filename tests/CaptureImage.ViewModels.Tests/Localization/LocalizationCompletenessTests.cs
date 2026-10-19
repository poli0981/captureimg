// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Localization;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Localization;

/// <summary>
/// Every shipped language defines exactly the English key set — read without parent
/// fallback, so an English string can't hide a missing translation — keeps the same
/// <c>{0}</c> placeholders, and every key the code and XAML use exists (and nothing else).
/// </summary>
public sealed partial class LocalizationCompletenessTests
{
    private const string BaseName = "CaptureImage.ViewModels.Resources.Strings.Strings";

    private static readonly ResourceManager Resources = new(BaseName, typeof(ResxLocalizationService).Assembly);

    public static TheoryData<string> Cultures => ["vi", "ar", "ja", "zh-Hans", "ko", "es", "fr", "de", "pt-BR", "ru"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_DefinesExactlyTheEnglishKeys(string culture)
    {
        var english = Entries(CultureInfo.InvariantCulture);
        var translated = Entries(CultureInfo.GetCultureInfo(culture));

        english.Keys.Except(translated.Keys).Should().BeEmpty("{0} must translate every key", culture);
        translated.Keys.Except(english.Keys).Should().BeEmpty("{0} must not define keys English lacks", culture);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_KeepsTheFormatPlaceholders(string culture)
    {
        var english = Entries(CultureInfo.InvariantCulture);
        var translated = Entries(CultureInfo.GetCultureInfo(culture));

        foreach (var (key, value) in translated)
        {
            Placeholders(value).Should().BeEquivalentTo(Placeholders(english[key]), "{0}/{1} is passed to string.Format", culture, key);
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_HasNoEmptyValues(string culture)
    {
        Entries(CultureInfo.GetCultureInfo(culture))
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .Should().BeEmpty();
    }

    [Fact]
    public void EveryKeyTheSourceUses_Exists()
    {
        var english = Entries(CultureInfo.InvariantCulture);

        UsedKeys().Where(key => !english.ContainsKey(key)).Should().BeEmpty();
    }

    [Fact]
    public void EveryKey_IsUsed()
    {
        var used = UsedKeys();

        Entries(CultureInfo.InvariantCulture).Keys.Where(key => !used.Contains(key)).Should().BeEmpty();
    }

    private static Dictionary<string, string> Entries(CultureInfo culture)
    {
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No resources for {culture.Name}.");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);
    }

    private static IEnumerable<string> Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(m => m.Value).Order(StringComparer.Ordinal);

    /// <summary>Keys referenced literally in src (C# and XAML) plus the ones built from enum names.</summary>
    private static HashSet<string> UsedKeys()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var src = Path.Combine(RepositoryRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Contains("bin") || parts.Contains("obj"))
            {
                continue;
            }

            var pattern = Path.GetExtension(file) switch
            {
                ".xaml" => XamlKeyPattern(),
                ".cs" => CodeKeyPattern(),
                _ => null,
            };
            if (pattern is null)
            {
                continue;
            }

            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                used.Add(match.Groups["key"].Value);
            }
        }

        // Keys composed at runtime from enum or setting names.
        used.UnionWith(Enum.GetNames<CaptureError>().Select(name => $"Error_{name}"));
        used.UnionWith(Enum.GetNames<CaptureMode>().Select(name => $"Mode_{name}"));
        used.UnionWith(Enum.GetNames<CaptureMode>().Select(name => $"Settings_Hotkey{name}Description"));
        used.UnionWith(Enum.GetNames<UpdateStatus>().Select(name => $"Update_Status_{name}"));
        used.UnionWith(Enum.GetValues<HotkeyValidationResult>()
            .Where(result => result != HotkeyValidationResult.Ok)
            .Select(result => $"Hotkey_Validation_{result}"));
        used.UnionWith(SettingValues.Themes.Select(theme => $"Settings_Theme{theme}"));
        used.UnionWith(SettingValues.LogLevels.Select(level => $"Settings_LogLevel{level}"));
        used.UnionWith(SettingValues.CountdownChoices.Where(s => s > 0).Select(s => $"Settings_Countdown{s}s"));
        used.Add("Settings_CountdownOff");
        return used;
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CaptureImage.sln")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("CaptureImage.sln not found above the test output folder.");
    }

    [GeneratedRegex(@"\{\d+(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{loc:Tr\s+(?<key>[A-Za-z0-9_]+)\}")]
    private static partial Regex XamlKeyPattern();

    // Localization["Key"], _localization["Key"], .Format("Key", …), LocalizedOption.Create(…, "Key"),
    // LocalizationSource.Bind("Key") and the tray menu's Item("Key", …).
    [GeneratedRegex(@"(?:[Ll]ocalization\[|\.Format\(|LocalizationSource\.Bind\(|Service\?\[|\bItem\(|localization,\s*)""(?<key>[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+)""")]
    private static partial Regex CodeKeyPattern();
}
